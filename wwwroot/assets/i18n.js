const translations = {
    "en": {
        "btnSetHotkey": "Set hotkey",
        "btnOpenItemSelector": "Select applications",
        "cpsLabel": "Clicks per second",
        "dutyLabel": "Click duty",
        "mouseLabel": "Mouse button",
        "holdModeLabel": "Hold to activate (off = toggle)",
        "mouseLeft": "Left",
        "mouseMiddle": "Middle",
        "mouseRight": "Right",
        "langToggle": "DE / EN",
        "selectorLoading": "Loading applications...",
        "selectorHint": "No selection means application filtering is off.",
        "selectorApplication": "Application",
        "selectorEmpty": "No running applications with visible windows were found.",
        "selectorPrompt": "Select the applications where clicking is allowed.",
        "selectorApplicationExited": "The application has exited. Reopen the selector to update the list.",
        "selectorSelectionExpired": "The selected application exited. Application filtering was updated.",
        "selectorUpdateFailed": "The selector could not process an application update."
    },
    "de": {
        "btnSetHotkey": "Tastenkombination setzen",
        "btnOpenItemSelector": "Anwendungen auswaehlen",
        "cpsLabel": "Klicks pro Sekunde",
        "dutyLabel": "Klickgrad",
        "mouseLabel": "Maustaste",
        "holdModeLabel": "Halten zum Aktivieren (aus = Umschalten)",
        "mouseLeft": "Links",
        "mouseMiddle": "Mitte",
        "mouseRight": "Rechts",
        "langToggle": "DE / EN",
        "selectorLoading": "Anwendungen werden geladen...",
        "selectorHint": "Wenn nichts ausgewählt ist, ist der Anwendungsfilter deaktiviert.",
        "selectorApplication": "Anwendung",
        "selectorEmpty": "Keine laufenden Anwendungen mit sichtbaren Fenstern gefunden.",
        "selectorPrompt": "Wähle die Anwendungen aus, in denen geklickt werden darf.",
        "selectorApplicationExited": "Die Anwendung wurde beendet. Öffne die Auswahl erneut, um die Liste zu aktualisieren.",
        "selectorSelectionExpired": "Die ausgewählte Anwendung wurde beendet. Der Anwendungsfilter wurde aktualisiert.",
        "selectorUpdateFailed": "Die Anwendungsauswahl konnte eine Aktualisierung nicht verarbeiten."
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

function translate(key) {
    var language = translations[currentLang] || translations.en;
    return language[key] || translations.en[key] || key;
}

function applyTranslations() {
    var elements = document.querySelectorAll("[data-i18n]");

    for (var i = 0; i < elements.length; i++) {
        var elemnt = elements[i];
        var key = elemnt.getAttribute("data-i18n");
        var value = translate(key);
        if (typeof value === "string") {
            elemnt.innerText = value;
        }
    }
}

document.addEventListener("DOMContentLoaded", function () {
    loadTranslations();
});
