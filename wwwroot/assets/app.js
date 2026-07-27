function setCps() {
    const cpsValue = document.querySelector('input[name="cpsinput"]').value;
    window.external.sendMessage(`setCps:${cpsValue}`);
}

function setClickDuty() {
    const clickdutyValue = document.querySelector('input[name="clickDutyinput"]').value;
    window.external.sendMessage(`setClickDuty:${clickdutyValue}`);
}

function setMouseButton(value) {
    window.external.sendMessage(`setMouseButton:${value}`);
}

function setKeybind() {
    const btn = document.getElementById('btnSetHotkey');
    btn.disabled = true;
    btn.innerText = "Dr�cke eine Tastenkombination..."
    window.external.sendMessage("setKeybind");
}

function setHoldMode() {
    const cb = document.getElementById('holdModeCheckbox');
    window.external.sendMessage(`setHoldMode:${cb.checked}`);
}

function openItemSelector() {
    window.external.sendMessage("openItemSelector");
}

document.addEventListener("DOMContentLoaded", function () {
    window.external.sendMessage("mainReady");
});

window.external.receiveMessage((message) => {
    try {
        const data = JSON.parse(message);

        if (data.type === "title") {
            const h = document.querySelector("h1");
            if (h) h.innerText = data.text;
            return;
        }

        if (data.type === "status") {
            document.getElementById("status").style.backgroundColor =
                data.state === 1 ? "#0F0" : "#F00";
            return;
        }

        if (data.type === "processFilterCapability") {
            const button = document.getElementById("btnOpenItemSelector");
            const warning = document.getElementById("processFilterWarning");

            button.disabled = !data.isAvailable;
            warning.hidden = data.isAvailable;
            warning.textContent = data.isAvailable ? "" : data.message;
            return;
        }

        if (data.type === "cps") {
            const el = document.getElementById("cps");
            if (el) el.value = data.text;
            return;
        }

        if (data.type === "clickDuty") {
            const el = document.getElementById("clickDuty");
            if (el) el.value = data.text;
            return;
        }

        if (data.type === "mouseButton") {
            const value = parseInt(data.value);
            if (value === 0) document.getElementById("mbLeft").checked = true;
            else if (value === 1) document.getElementById("mbMiddle").checked = true;
            else if (value === 2) document.getElementById("mbRight").checked = true;
            return;
        }

        if (data.type === "keybind") {
            const btn = document.getElementById('btnSetHotkey');
            btn.innerText = data.text.replace(/^Hotkey set to:\s*/, "Hotkey: ");

            if (/^Hotkey set to:/i.test(data.text))
                btn.disabled = false;

            return;
        }

        if (data.type === "holdMode") {
            const cb = document.getElementById('holdModeCheckbox');
            if (cb !== null && cb !== undefined) {
                cb.checked = data.enabled ? true : false;
            }
            return;
        }

        if (data.type === "language") {
            currentLang = data.lang || "en";
            applyTranslations();
            return;
        }
    } catch (ex) {
        console.log("Error:", ex);
    }

    if (typeof message === "string" && message.startsWith("TITLE:")) {
        const h = document.querySelector("h1");
        if (h) h.innerText = message.slice(6);
        return;
    }

    console.log("Message from backend:", message);
});
