use crate::{Error, Result};
use serde::{Deserialize, Serialize};
use std::collections::HashSet;

fn one() -> u32 { 1 }
fn yes() -> bool { true }
fn dwell() -> u32 { 150 }
fn hold_ms() -> u32 { 350 }

#[derive(Serialize, Deserialize, Clone, Debug, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct Config {
    #[serde(default = "one")]
    pub version: u32,
    pub trigger: Trigger,
    #[serde(default)]
    pub theme: serde_json::Value,
    #[serde(default)]
    pub behavior: Behavior,
    pub menu: Vec<Node>,
}

#[derive(Serialize, Deserialize, Clone, Copy, Debug, PartialEq, Eq, Default)]
#[serde(rename_all = "camelCase")]
pub enum TriggerMode {
    /// Open immediately on press.
    #[default]
    Press,
    /// Open only after the key/button has been held for `hold_ms`.
    Hold,
}

#[derive(Serialize, Deserialize, Clone, Copy, Debug, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub enum MouseButton { Left, Right, Middle, Back, Forward }

#[derive(Serialize, Deserialize, Clone, Debug, PartialEq)]
#[serde(tag = "type", rename_all = "camelCase")]
pub enum Trigger {
    #[serde(rename_all = "camelCase")]
    Keyboard {
        shortcut: String,
        #[serde(default)]
        mode: TriggerMode,
        #[serde(default = "hold_ms")]
        hold_ms: u32,
    },
    #[serde(rename_all = "camelCase")]
    Mouse {
        button: MouseButton,
        #[serde(default)]
        mode: TriggerMode,
        #[serde(default = "hold_ms")]
        hold_ms: u32,
    },
}

#[derive(Serialize, Deserialize, Clone, Debug, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct Behavior {
    /// How long the pointer must rest on a slice before it fires. 0 = instant.
    #[serde(default = "dwell")]
    pub dwell_ms: u32,
    /// Re-centre the cursor when a new layer opens.
    #[serde(default = "yes")]
    pub warp_cursor: bool,
}
impl Default for Behavior {
    fn default() -> Self { Self { dwell_ms: dwell(), warp_cursor: true } }
}

#[derive(Serialize, Deserialize, Clone, Debug, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct Node {
    pub id: String,
    #[serde(default)]
    pub label: Option<String>,
    #[serde(default)]
    pub icon: Option<String>,
    #[serde(default)]
    pub image: Option<String>,
    #[serde(default)]
    pub display: Option<String>,
    #[serde(default)]
    pub color: Option<String>,
    /// A node with children is a folder (opens the next layer); otherwise it runs `action`.
    #[serde(default)]
    pub children: Option<Vec<Node>>,
    #[serde(default)]
    pub action: Option<Action>,
}

#[derive(Serialize, Deserialize, Clone, Debug, PartialEq)]
#[serde(tag = "type", rename_all = "camelCase")]
pub enum Action {
    /// Open a file/folder/app with its default handler.
    Open { path: String },
    /// Open a remote-desktop file with `mstsc.exe`.
    #[serde(rename_all = "camelCase")]
    Rdp { file: String, #[serde(default)] args: Vec<String> },
    /// Start any program.
    #[serde(rename_all = "camelCase")]
    Run {
        command: String,
        #[serde(default)] args: Vec<String>,
        #[serde(default)] cwd: Option<String>,
        #[serde(default)] hidden: bool,
    },
    /// Open a URL in the default browser.
    Url { url: String },
    /// Open the settings window.
    Settings,
}

impl Config {
    pub fn from_json(s: &str) -> Result<Config> {
        let c: Config = serde_json::from_str(s)?;
        c.validate()?;
        Ok(c)
    }

    pub fn validate(&self) -> Result<()> {
        if let Trigger::Keyboard { shortcut, .. } = &self.trigger {
            if shortcut.trim().is_empty() {
                return Err(Error::Config("trigger.shortcut is empty".into()));
            }
        }
        let mut seen = HashSet::new();
        fn walk(nodes: &[Node], seen: &mut HashSet<String>) -> Result<()> {
            for n in nodes {
                if n.id.trim().is_empty() {
                    return Err(Error::Config("a menu node has an empty id".into()));
                }
                if !seen.insert(n.id.clone()) {
                    return Err(Error::Config(format!("duplicate node id `{}`", n.id)));
                }
                if let Some(c) = &n.children {
                    walk(c, seen)?;
                }
            }
            Ok(())
        }
        walk(&self.menu, &mut seen)
    }

    pub fn find_node(&self, id: &str) -> Option<&Node> {
        fn go<'a>(nodes: &'a [Node], id: &str) -> Option<&'a Node> {
            for n in nodes {
                if n.id == id { return Some(n); }
                if let Some(f) = n.children.as_deref().and_then(|c| go(c, id)) { return Some(f); }
            }
            None
        }
        go(&self.menu, id)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    const EXAMPLE: &str = include_str!("../../../config.example.json");

    #[test]
    fn example_config_is_valid() {
        let c = Config::from_json(EXAMPLE).expect("config.example.json must parse");
        assert!(c.find_node("rdp-admin-1").is_some());
        assert!(matches!(c.trigger, Trigger::Keyboard { .. }));
    }

    #[test]
    fn duplicate_ids_rejected() {
        let j = r#"{"trigger":{"type":"keyboard","shortcut":"Ctrl+Shift+Z"},
          "menu":[{"id":"a"},{"id":"a"}]}"#;
        assert!(Config::from_json(j).is_err());
    }

    #[test]
    fn mouse_trigger_and_defaults() {
        let j = r#"{"trigger":{"type":"mouse","button":"middle","mode":"hold"},"menu":[]}"#;
        let c = Config::from_json(j).unwrap();
        assert_eq!(c.behavior, Behavior::default());
        assert!(matches!(c.trigger, Trigger::Mouse { button: MouseButton::Middle, mode: TriggerMode::Hold, hold_ms: 350 }));
    }

    #[test]
    fn unknown_action_type_rejected() {
        let j = r#"{"trigger":{"type":"keyboard","shortcut":"F1"},
          "menu":[{"id":"a","action":{"type":"format-disk"}}]}"#;
        assert!(Config::from_json(j).is_err());
    }
}
