//! GUI-free core of productivity.larp: the config model, `${ENV}` expansion
//! and the action runner. Everything here is unit-testable on any OS.

pub mod action;
pub mod config;
pub mod env;
pub mod image;

pub use action::{Prepared, run_prepared};
pub use config::{Action, Config, Node};

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("invalid config: {0}")]
    Config(String),
    #[error("environment variable `{0}` is not set (define it in .env)")]
    MissingEnv(String),
    #[error("unterminated `${{` in `{0}`")]
    BadTemplate(String),
    #[error("{0}")]
    Io(#[from] std::io::Error),
    #[error("{0}")]
    Json(#[from] serde_json::Error),
    #[error("{0}")]
    Other(String),
}

pub type Result<T> = std::result::Result<T, Error>;
