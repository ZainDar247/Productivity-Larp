import React from "react";
import ReactDOM from "react-dom/client";
import { Overlay } from "./overlay/Overlay";
import { SettingsApp } from "./settings/SettingsApp";
import { bridge } from "./lib/bridge";
import "./styles.css";

// the MVC page sets data-view; ?view= is for the plain-browser dev mode
const view = document.documentElement.dataset.view || new URLSearchParams(location.search).get("view") || "settings";
document.documentElement.dataset.view = view;
if (!bridge.native) document.documentElement.dataset.mock = "1";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>{view === "overlay" ? <Overlay /> : <SettingsApp />}</React.StrictMode>,
);
