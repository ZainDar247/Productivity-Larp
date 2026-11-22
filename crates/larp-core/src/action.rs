use crate::config::Action;
use crate::env::expand;
use crate::{Error, Result};

/// A fully-resolved action (env vars expanded), ready to execute.
#[derive(Debug, PartialEq, Eq)]
pub enum Prepared {
    Open(String),
    Url(String),
    Spawn { program: String, args: Vec<String>, cwd: Option<String>, hidden: bool },
    Settings,
}

impl Action {
    pub fn prepare(&self, lookup: &dyn Fn(&str) -> Option<String>) -> Result<Prepared> {
        let ex = |s: &str| expand(s, lookup);
        Ok(match self {
            Action::Open { path } => Prepared::Open(ex(path)?),
            Action::Url { url } => Prepared::Url(ex(url)?),
            Action::Rdp { file, args } => {
                let mut a = vec![ex(file)?];
                for x in args { a.push(ex(x)?); }
                Prepared::Spawn { program: "mstsc.exe".into(), args: a, cwd: None, hidden: false }
            }
            Action::Run { command, args, cwd, hidden } => Prepared::Spawn {
                program: ex(command)?,
                args: args.iter().map(|a| ex(a)).collect::<Result<_>>()?,
                cwd: cwd.as_deref().map(ex).transpose()?,
                hidden: *hidden,
            },
            Action::Settings => Prepared::Settings,
        })
    }
}

/// Perform the side effect. `Settings` is handled by the GUI layer, not here.
pub fn run_prepared(p: &Prepared) -> Result<()> {
    match p {
        Prepared::Open(target) | Prepared::Url(target) => {
            if target.trim().is_empty() {
                return Err(Error::Other("nothing to open (empty path)".into()));
            }
            open::that_detached(target).map_err(Error::Io)
        }
        Prepared::Spawn { program, args, cwd, hidden } => {
            let mut cmd = std::process::Command::new(program);
            cmd.args(args);
            if let Some(d) = cwd { cmd.current_dir(d); }
            #[cfg(windows)]
            if *hidden {
                use std::os::windows::process::CommandExt;
                cmd.creation_flags(0x0800_0000); // CREATE_NO_WINDOW
            }
            #[cfg(not(windows))]
            let _ = hidden;
            cmd.spawn().map(|_| ()).map_err(|e| Error::Other(format!("could not start `{program}`: {e}")))
        }
        Prepared::Settings => Ok(()),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn look(k: &str) -> Option<String> {
        (k == "RDP_DIR").then(|| r"C:\rdp".to_string())
    }

    #[test]
    fn rdp_uses_mstsc_with_expanded_path() {
        let a = Action::Rdp { file: r"${RDP_DIR}\admin.rdp".into(), args: vec!["/admin".into()] };
        assert_eq!(
            a.prepare(&look).unwrap(),
            Prepared::Spawn { program: "mstsc.exe".into(), args: vec![r"C:\rdp\admin.rdp".into(), "/admin".into()], cwd: None, hidden: false }
        );
    }

    #[test]
    fn run_expands_every_field() {
        let a = Action::Run { command: "x".into(), args: vec!["${RDP_DIR}".into()], cwd: Some("${RDP_DIR}".into()), hidden: true };
        let Prepared::Spawn { args, cwd, hidden, .. } = a.prepare(&look).unwrap() else { panic!() };
        assert_eq!(args, vec![r"C:\rdp"]);
        assert_eq!(cwd.as_deref(), Some(r"C:\rdp"));
        assert!(hidden);
    }

    #[test]
    fn missing_env_fails_before_launch() {
        let a = Action::Open { path: "${SECRET_PATH}".into() };
        assert!(a.prepare(&look).is_err());
    }

    #[test]
    fn spawn_failure_is_reported() {
        let p = Prepared::Spawn { program: "definitely-not-a-real-binary-xyz".into(), args: vec![], cwd: None, hidden: false };
        assert!(run_prepared(&p).is_err());
    }

    #[test]
    fn empty_open_rejected() {
        assert!(run_prepared(&Prepared::Open("  ".into())).is_err());
    }
}
