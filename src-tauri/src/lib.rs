mod commands;
mod state;
mod trigger;
mod windows;

use state::AppState;
use tauri::{
    menu::{Menu, MenuItem},
    tray::TrayIconBuilder,
    Manager,
};

pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| windows::open_settings(app)))
        .plugin(tauri_plugin_global_shortcut::Builder::new().build())
        .plugin(tauri_plugin_dialog::init())
        .invoke_handler(tauri::generate_handler![
            commands::get_config,
            commands::save_config,
            commands::run_node,
            commands::close_overlay,
            commands::warp_cursor,
            commands::load_image,
            commands::open_settings,
            commands::open_config_dir,
        ])
        .setup(|app| {
            let dir = match std::env::var_os("LARP_CONFIG_DIR") {
                Some(d) => d.into(),
                None => app.path().app_config_dir()?,
            };
            let st = AppState::init(dir);
            let first_run = st.first_run;
            let cfg = st.config.read().unwrap().clone();
            app.manage(st);

            windows::create_overlay(app.handle())?;

            // Tray icon: always a way back into Settings, even if the menu is misconfigured.
            let settings = MenuItem::with_id(app, "settings", "Settings", true, None::<&str>)?;
            let quit = MenuItem::with_id(app, "quit", "Quit", true, None::<&str>)?;
            let menu = Menu::with_items(app, &[&settings, &quit])?;
            let mut tray = TrayIconBuilder::new()
                .tooltip("productivity.larp")
                .menu(&menu)
                .on_menu_event(|app, ev| match ev.id().as_ref() {
                    "settings" => windows::open_settings(app),
                    "quit" => app.exit(0),
                    _ => {}
                });
            if let Some(icon) = app.default_window_icon() {
                tray = tray.icon(icon.clone());
            }
            tray.build(app)?;

            if let Err(e) = trigger::apply(app.handle(), &cfg) {
                eprintln!("trigger not registered: {e}");
                windows::open_settings(app.handle()); // let the user fix it
            } else if first_run {
                windows::open_settings(app.handle());
            }
            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("error while running productivity.larp");
}
