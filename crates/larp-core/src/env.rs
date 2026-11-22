use crate::{Error, Result};

/// Replace `${NAME}` with the value from `lookup`. `$$` is a literal `$`.
/// A missing variable is an error so a typo never silently launches the wrong thing.
pub fn expand(input: &str, lookup: &dyn Fn(&str) -> Option<String>) -> Result<String> {
    let mut out = String::with_capacity(input.len());
    let mut chars = input.char_indices().peekable();
    while let Some((i, c)) = chars.next() {
        if c != '$' {
            out.push(c);
            continue;
        }
        match chars.peek() {
            Some((_, '$')) => {
                chars.next();
                out.push('$');
            }
            Some((_, '{')) => {
                chars.next();
                let start = i + 2;
                let rest = &input[start..];
                let end = rest.find('}').ok_or_else(|| Error::BadTemplate(input.into()))?;
                let name = rest[..end].trim();
                let val = lookup(name).ok_or_else(|| Error::MissingEnv(name.into()))?;
                out.push_str(&val);
                // skip past the consumed name and closing brace
                while let Some(&(j, _)) = chars.peek() {
                    if j <= start + end {
                        chars.next();
                    } else {
                        break;
                    }
                }
            }
            _ => out.push('$'),
        }
    }
    Ok(out)
}

/// Parse a dotenv-style body (`KEY=value`, `#` comments, optional quotes).
pub fn parse_dotenv(body: &str) -> Vec<(String, String)> {
    body.lines()
        .filter_map(|line| {
            let line = line.trim();
            if line.is_empty() || line.starts_with('#') {
                return None;
            }
            let line = line.strip_prefix("export ").unwrap_or(line);
            let (k, v) = line.split_once('=')?;
            let v = v.trim();
            let v = v
                .strip_prefix('"')
                .and_then(|s| s.strip_suffix('"'))
                .or_else(|| v.strip_prefix('\'').and_then(|s| s.strip_suffix('\'')))
                .unwrap_or(v);
            Some((k.trim().to_string(), v.to_string()))
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    fn look(k: &str) -> Option<String> {
        match k {
            "A" => Some("alpha".into()),
            "DIR" => Some(r"C:\rdp".into()),
            _ => None,
        }
    }

    #[test]
    fn expands_vars() {
        assert_eq!(expand(r"${DIR}\admin\${A}.rdp", &look).unwrap(), r"C:\rdp\admin\alpha.rdp");
    }
    #[test]
    fn plain_and_escaped() {
        assert_eq!(expand("no vars", &look).unwrap(), "no vars");
        assert_eq!(expand("cost $$5 and $x", &look).unwrap(), "cost $5 and $x");
    }
    #[test]
    fn missing_var_errors() {
        assert!(matches!(expand("${NOPE}", &look), Err(Error::MissingEnv(n)) if n == "NOPE"));
    }
    #[test]
    fn unterminated_errors() {
        assert!(matches!(expand("${A", &look), Err(Error::BadTemplate(_))));
    }
    #[test]
    fn dotenv() {
        let v = parse_dotenv("# c\nA=1\nexport B=\"two words\"\n\nC='x'\nbad");
        assert_eq!(v, vec![("A".into(), "1".into()), ("B".into(), "two words".into()), ("C".into(), "x".into())]);
    }
}
