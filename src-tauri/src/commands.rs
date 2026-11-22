use crate::{state::AppState, trigger, windows};
use larp_core::{run_prepared, Config, Prepared};
use serde::Serialize;
use tauri::{AppHandle, Emitter, State, WebviewWindow};

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ConfigPayload {
    /// Config file text exactly as stored.
    raw: String,
    /// Why the stored config could not be used (the app then runs on defaults).
    error: Option<String>,
    config_dir: String,
    first_run: bool,
}

#[tauri::command]
pub fn get_config(state: State<AppState>) -> ConfigPayload {
    ConfigPayload {
        raw: state.raw_config(),
        error: state.load_error.lock().unwrap().clone(),
        config_dir: state.dir.display().to_string(),
        first_run: state.first_run,
    }
}

#[tauri::command]
pub fn save_config(app: AppHandle, state: State<AppState>, json: String) -> Result<(), String> {
    let new = Config::from_json(&json).map_err(|e| e.to_string())?;
    let old = state.config.read().unwrap().clone();
    // Register the new trigger first; if that fails keep the old one and don't save.
    if let Err(e) = trigger::apply(&app, &new) {
        let _ = trigger::apply(&app, &old);
        return Err(e);
    }
    std::fs::write(state.config_path(), &json).map_err(|e| e.to_string())?;
    *state.config.write().unwrap() = new;
    *state.load_error.lock().unwrap() = None;
    let _ = app.emit("config:changed", ());
    Ok(())
}

/// Run a menu node by id. The backend looks the action up in its own copy of
/// the config — the webview only ever says *which* item was chosen.
#[tauri::command]
pub fn run_node(app: AppHandle, state: State<AppState>, id: String) -> Result<(), String> {
    let action = {
        let cfg = state.config.read().unwrap();
        let node = cfg.find_node(&id).ok_or_else(|| format!("unknown menu item `{id}`"))?;
        node.action.clone().ok_or_else(|| "this item has no action yet".to_string())?
    };
    let prepared = action.prepare(&state.env_lookup()).map_err(|e| e.to_string())?;
    match prepared {
        Prepared::Settings => windows::open_settings(&app),
        other => run_prepared(&other).map_err(|e| e.to_string())?,
    }
    windows::close_overlay(&app);
    Ok(())
}

#[tauri::command]
pub fn close_overlay(app: AppHandle) {
    windows::close_overlay(&app);
}

/// Move the pointer (coordinates in the calling window, CSS px).
#[tauri::command]
pub fn warp_cursor(window: WebviewWindow, x: f64, y: f64) -> Result<(), String> {
    window.set_cursor_position(tauri::LogicalPosition::new(x, y)).map_err(|e| e.to_string())
}

#[tauri::command]
pub fn load_image(path: String) -> Result<String, String> {
    larp_core::image::data_url(&path).map_err(|e| e.to_string())
}

#[tauri::command]
pub fn open_settings(app: AppHandle) {
    windows::open_settings(&app);
}

#[tauri::command]
pub fn open_config_dir(state: State<AppState>) -> Result<(), String> {
    larp_core::run_prepared(&Prepared::Open(state.dir.display().to_string())).map_err(|e| e.to_string())
}
