const translations = {
    "en": {
        "btnSetHotkey": "Set hotkey",
        "cpsLabel": "Clicks per second",
        "dutyLabel": "Click duty",
        "mouseLabel": "Mouse button",
        "holdModeLabel": "Hold to activate (off = toggle)",
        "mouseLeft": "Left",
        "mouseMiddle": "Middle",
        "mouseRight": "Right",
        "langToggle": "DE / EN"
    },
    "de": {
        "btnSetHotkey": "Tastenkombination setzen",
        "cpsLabel": "Klicks pro Sekunde",
        "dutyLabel": "Klickgrad",
        "mouseLabel": "Maustaste",
        "holdModeLabel": "Halten zum Aktivieren (aus = Umschalten)",
        "mouseLeft": "Links",
        "mouseMiddle": "Mitte",
        "mouseRight": "Rechts",
        "langToggle": "DE / EN"
    }
};

var currentLang = "en";

function loadTranslations() {
    try {
        applyTranslations();
    } catch (e) {
        console.log("Failed to parse translations:", e);
    }
}

function switchLang() {
    currentLang = currentLang === "de" ? "en" : "de";
    window.external.sendMessage(`setLanguage:${currentLang}`);
    applyTranslations();
}

function applyTranslations() {
    var t = translations[currentLang] || {};
    var elements = document.querySelectorAll("[data-i18n]");

    for (var i = 0; i < elements.length; i++) {
        var elemnt = elements[i];
        var key = elemnt.getAttribute("data-i18n");
        var value = t[key];
        if (typeof value === "string") {
            elemnt.innerText = value;
        }
    }
}

document.addEventListener("DOMContentLoaded", function () {
    loadTranslations();
});
