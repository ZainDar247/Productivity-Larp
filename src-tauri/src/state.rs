use larp_core::{env::parse_dotenv, Config};
use std::{
    collections::HashMap,
    fs,
    path::PathBuf,
    sync::{Mutex, RwLock},
    time::Instant,
};

pub const DEFAULT_CONFIG: &str = include_str!("../../config.example.json");

/// Debounce state for the trigger (key auto-repeat, hold detection).
pub struct Gate {
    pub down: bool,
    pub generation: u64,
    pub last_event: Instant,
}

pub struct AppState {
    pub dir: PathBuf,
    pub config: RwLock<Config>,
    pub load_error: Mutex<Option<String>>,
    pub first_run: bool,
    pub gate: Mutex<Gate>,
    pub opened_at: Mutex<Instant>,
}

impl AppState {
    pub fn init(dir: PathBuf) -> Self {
        let _ = fs::create_dir_all(&dir);
        let path = dir.join("config.json");
        let first_run = !path.exists();
        if first_run {
            let _ = fs::write(&path, DEFAULT_CONFIG);
        }
        let parsed = fs::read_to_string(&path)
            .map_err(|e| e.to_string())
            .and_then(|s| Config::from_json(&s).map_err(|e| e.to_string()));
        let (config, err) = match parsed {
            Ok(c) => (c, None),
            // Keep the user's broken file untouched; run on defaults so the app still starts.
            Err(e) => (Config::from_json(DEFAULT_CONFIG).expect("bundled default is valid"), Some(e)),
        };
        Self {
            dir,
            config: RwLock::new(config),
            load_error: Mutex::new(err),
            first_run,
            gate: Mutex::new(Gate { down: false, generation: 0, last_event: Instant::now() }),
            opened_at: Mutex::new(Instant::now()),
        }
    }

    pub fn config_path(&self) -> PathBuf {
        self.dir.join("config.json")
    }

    /// Raw config text exactly as on disk (so the settings UI can repair a broken file).
    pub fn raw_config(&self) -> String {
        fs::read_to_string(self.config_path()).unwrap_or_else(|_| DEFAULT_CONFIG.to_string())
    }

    /// Variables from `<config dir>/.env`, then `./.env`, then the real environment.
    /// Re-read on every action so edits to `.env` apply without a restart.
    pub fn env_lookup(&self) -> impl Fn(&str) -> Option<String> {
        let mut vars: HashMap<String, String> = HashMap::new();
        for p in [self.dir.join(".env"), PathBuf::from(".env")] {
            if let Ok(body) = fs::read_to_string(p) {
                for (k, v) in parse_dotenv(&body) {
                    vars.entry(k).or_insert(v);
                }
            }
        }
        move |k: &str| vars.get(k).cloned().or_else(|| std::env::var(k).ok())
    }
}
