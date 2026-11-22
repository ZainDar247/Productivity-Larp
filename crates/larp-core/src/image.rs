use crate::{Error, Result};
use base64::{engine::general_purpose::STANDARD, Engine};
use std::path::Path;

const MAX_BYTES: u64 = 8 * 1024 * 1024;

/// Read an image file and return it as a `data:` URL the webview can show.
pub fn data_url(path: &str) -> Result<String> {
    let p = Path::new(path);
    let ext = p.extension().and_then(|e| e.to_str()).unwrap_or("").to_ascii_lowercase();
    let mime = match ext.as_str() {
        "png" => "image/png",
        "jpg" | "jpeg" => "image/jpeg",
        "gif" => "image/gif",
        "webp" => "image/webp",
        "svg" => "image/svg+xml",
        "ico" => "image/x-icon",
        "bmp" => "image/bmp",
        _ => return Err(Error::Other(format!("unsupported image type `.{ext}`"))),
    };
    if std::fs::metadata(p)?.len() > MAX_BYTES {
        return Err(Error::Other("image larger than 8 MB".into()));
    }
    let bytes = std::fs::read(p)?;
    Ok(format!("data:{mime};base64,{}", STANDARD.encode(bytes)))
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn rejects_unknown_extension() {
        assert!(data_url("x.exe").is_err());
    }
    #[test]
    fn encodes_file() {
        let dir = std::env::temp_dir().join("larp_img_test.png");
        std::fs::write(&dir, [1u8, 2, 3]).unwrap();
        assert_eq!(data_url(dir.to_str().unwrap()).unwrap(), "data:image/png;base64,AQID");
        std::fs::remove_file(dir).ok();
    }
}
