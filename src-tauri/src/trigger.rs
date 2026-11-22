//! Opening the menu: a global keyboard shortcut or a mouse button, in
//! "press" or "hold" mode. All matching is driven by the live config.

use crate::{state::AppState, windows};
use larp_core::config::{Config, MouseButton, Trigger, TriggerMode};
use std::{
    sync::atomic::{AtomicBool, Ordering},
    time::{Duration, Instant},
};
use tauri::{AppHandle, Manager};
use tauri_plugin_global_shortcut::{GlobalShortcutExt, ShortcutState};

/// If no key event arrives for this long we assume the key was released
/// (some platforms never report key-up for hotkeys).
const STALE: Duration = Duration::from_millis(700);

static MOUSE_LISTENER_STARTED: AtomicBool = AtomicBool::new(false);

/// (Re)register the trigger described by `cfg`. Returns an error if the
/// shortcut can't be registered (invalid string, already taken by another app…).
pub fn apply(app: &AppHandle, cfg: &Config) -> Result<(), String> {
    let gs = app.global_shortcut();
    gs.unregister_all().map_err(|e| e.to_string())?;
    match &cfg.trigger {
        Trigger::Keyboard { shortcut, .. } => {
            gs.on_shortcut(shortcut.as_str(), |app, _sc, ev| {
                handle(app, ev.state() == ShortcutState::Pressed);
            })
            .map_err(|e| format!("cannot register `{shortcut}`: {e}"))?;
        }
        Trigger::Mouse { .. } => start_mouse_listener(app),
    }
    Ok(())
}

/// The low-level mouse hook is only installed once a mouse trigger is chosen,
/// so keyboard-only users pay no cost for it.
fn start_mouse_listener(app: &AppHandle) {
    if MOUSE_LISTENER_STARTED.swap(true, Ordering::SeqCst) {
        return;
    }
    let app = app.clone();
    std::thread::spawn(move || {
        let res = rdev::listen(move |ev| {
            let (btn, pressed) = match ev.event_type {
                rdev::EventType::ButtonPress(b) => (b, true),
                rdev::EventType::ButtonRelease(b) => (b, false),
                _ => return,
            };
            let want = {
                let st = app.state::<AppState>();
                let cfg = st.config.read().unwrap();
                match &cfg.trigger {
                    Trigger::Mouse { button, .. } => Some(*button),
                    _ => None,
                }
            };
            if want.map(|w| matches_button(w, btn)).unwrap_or(false) {
                handle(&app, pressed);
            }
        });
        if let Err(e) = res {
            eprintln!("mouse hook failed: {e:?}");
            MOUSE_LISTENER_STARTED.store(false, Ordering::SeqCst);
        }
    });
}

fn matches_button(want: MouseButton, got: rdev::Button) -> bool {
    use rdev::Button as B;
    matches!(
        (want, got),
        (MouseButton::Left, B::Left)
            | (MouseButton::Right, B::Right)
            | (MouseButton::Middle, B::Middle)
            | (MouseButton::Back, B::Unknown(1))
            | (MouseButton::Forward, B::Unknown(2))
    )
}

fn mode_and_hold(app: &AppHandle) -> (TriggerMode, u32) {
    let st = app.state::<AppState>();
    let cfg = st.config.read().unwrap();
    match &cfg.trigger {
        Trigger::Keyboard { mode, hold_ms, .. } | Trigger::Mouse { mode, hold_ms, .. } => (*mode, *hold_ms),
    }
}

fn handle(app: &AppHandle, pressed: bool) {
    let st = app.state::<AppState>();
    if !pressed {
        if let Ok(mut g) = st.gate.lock() {
            g.down = false;
        }
        return;
    }
    let (mode, hold_ms) = mode_and_hold(app);
    let generation;
    {
        let mut g = st.gate.lock().unwrap();
        let repeat = g.down && g.last_event.elapsed() < STALE;
        g.last_event = Instant::now();
        if repeat {
            return; // auto-repeat while held
        }
        g.down = true;
        g.generation += 1;
        generation = g.generation;
    }
    match mode {
        TriggerMode::Press => windows::toggle_overlay(app),
        TriggerMode::Hold => {
            let app = app.clone();
            std::thread::spawn(move || {
                std::thread::sleep(Duration::from_millis(hold_ms as u64));
                let st = app.state::<AppState>();
                let still_held = st.gate.lock().map(|g| g.down && g.generation == generation).unwrap_or(false);
                if still_held && !windows::is_overlay_visible(&app) {
                    windows::open_overlay(&app);
                }
            });
        }
    }
}
