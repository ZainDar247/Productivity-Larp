use crate::state::AppState;
use serde::Serialize;
use tauri::{
    AppHandle, Emitter, Manager, PhysicalPosition, PhysicalSize, WebviewUrl, WebviewWindowBuilder,
};

#[derive(Serialize, Clone)]
struct OpenPayload {
    /// Cursor position inside the overlay, in CSS px.
    x: f64,
    y: f64,
    /// Overlay size in CSS px.
    w: f64,
    h: f64,
}

/// The overlay is created once, hidden, so opening it later is instant.
pub fn create_overlay(app: &AppHandle) -> tauri::Result<()> {
    WebviewWindowBuilder::new(app, "overlay", WebviewUrl::App("index.html?view=overlay".into()))
        .title("productivity.larp overlay")
        .transparent(true)
        .decorations(false)
        .shadow(false)
        .always_on_top(true)
        .skip_taskbar(true)
        .resizable(false)
        .visible(false)
        .focused(false)
        .build()?;
    Ok(())
}

pub fn open_overlay(app: &AppHandle) {
    let Some(win) = app.get_webview_window("overlay") else { return };
    let Ok(cur) = app.cursor_position() else { return };
    let mon = app
        .monitor_from_point(cur.x, cur.y)
        .ok()
        .flatten()
        .or_else(|| app.primary_monitor().ok().flatten());
    let Some(mon) = mon else { return };

    let scale = mon.scale_factor();
    let pos: PhysicalPosition<i32> = *mon.position();
    let size: PhysicalSize<u32> = *mon.size();
    let _ = win.set_position(pos);
    let _ = win.set_size(size);

    if let Ok(mut t) = app.state::<AppState>().opened_at.lock() {
        *t = std::time::Instant::now();
    }
    let payload = OpenPayload {
        x: (cur.x - pos.x as f64) / scale,
        y: (cur.y - pos.y as f64) / scale,
        w: size.width as f64 / scale,
        h: size.height as f64 / scale,
    };
    let _ = win.show();
    let _ = win.set_focus();
    let _ = app.emit_to("overlay", "menu:open", payload);
}

pub fn close_overlay(app: &AppHandle) {
    if let Some(win) = app.get_webview_window("overlay") {
        let _ = win.hide();
    }
    let _ = app.emit_to("overlay", "menu:close", ());
}

pub fn toggle_overlay(app: &AppHandle) {
    let visible = app
        .get_webview_window("overlay")
        .and_then(|w| w.is_visible().ok())
        .unwrap_or(false);
    if visible { close_overlay(app) } else { open_overlay(app) }
}

pub fn is_overlay_visible(app: &AppHandle) -> bool {
    app.get_webview_window("overlay").and_then(|w| w.is_visible().ok()).unwrap_or(false)
}

pub fn open_settings(app: &AppHandle) {
    close_overlay(app);
    if let Some(w) = app.get_webview_window("settings") {
        let _ = w.show();
        let _ = w.unminimize();
        let _ = w.set_focus();
        return;
    }
    let _ = WebviewWindowBuilder::new(app, "settings", WebviewUrl::App("index.html?view=settings".into()))
        .title("productivity.larp — Settings")
        .inner_size(1180.0, 780.0)
        .min_inner_size(860.0, 560.0)
        .center()
        .build();
}
