#Requires AutoHotkey v2.0
#SingleInstance Force
InstallKeybdHook
Persistent
global CFGDIR := EnvGet("ProgramData") "\n52te"
global INI := CFGDIR "\n52te_config.ini"
if !DirExist(CFGDIR)
try DirCreate(CFGDIR)
if (!FileExist(INI) && FileExist(A_ScriptDir "\n52te_config.ini"))
try FileCopy(A_ScriptDir "\n52te_config.ini", INI)
global LEARNREQ := CFGDIR "\aprender.req"
global LEARNRES := CFGDIR "\aprender.res"
global KBDLIST := CFGDIR "\teclados.txt"
try FileDelete(LEARNREQ)
try FileDelete(LEARNRES)
global selectedId := ""
global identifyMode := false
global learnMode := ""
global wizIdx := 0, wizNew := Map()
global COL_BG     := "0x1E1E1E"
global COL_PANEL  := "0x252526"
global COL_ASSIGN := "0x2D7D46"
global COL_NONE   := "0x3A3A3A"
global COL_SEL    := "0x0E639C"
global PHYS := Map(
"B01","Tab",  "B02","q", "B03","w", "B04","e", "B05","r",
"B06","CapsLock", "B07","a", "B08","s", "B09","d", "B10","f",
"B11","LShift", "B12","z", "B13","x", "B14","c",
"BESP","Space", "BALT","LAlt",
"BUP","Up", "BDOWN","Down", "BLEFT","Left", "BRIGHT","Right",
"BWHEEL","MButton")
global NICE := Map(
"B01","01", "B02","02", "B03","03", "B04","04", "B05","05",
"B06","06", "B07","07", "B08","08", "B09","09", "B10","10",
"B11","11", "B12","12", "B13","13", "B14","14",
"BESP","Thumb", "BALT","Round",
"BUP","Up", "BDOWN","Down", "BLEFT","Left", "BRIGHT","Right",
"BWHEEL","Wheel")
global FACTORY := Map(
"B01","Tab","B02","Q","B03","W","B04","E","B05","R",
"B06","Caps","B07","A","B08","S","B09","D","B10","F",
"B11","Shift","B12","Z","B13","X","B14","C",
"BESP","Space","BALT","Alt","BUP","↑","BDOWN","↓","BLEFT","←","BRIGHT","→",
"BWHEEL","Click")
; "none" must stay 5th: it is the fallback index used below.
global TYPES := ["key","send","text","run","none","keymap"]
global TYPELABELS := ["Key (held down)","Combo / macro","Type text","Open program or website","No change (original)","Switch keymap (Blue / Red / Green)"]
; Keymaps, as in the original Belkin software. Blue is the default and lives in the
; original [Botones] section, so configs from earlier versions keep working.
global LAYERS := ["Botones","Botones.Red","Botones.Green"]
global LAYERNAMES := ["Blue","Red","Green"]
global LAYERBG := ["0x1F5FBF","0xB0302A","0x2D7D46"]
global LAYERFG := ["4C8DFF","FF6A5F","4CC76A"]
global KEYMAPLABELS := ["Cycle keymaps (Blue, Red, Green)","Blue (toggle)","Red (toggle)","Green (toggle)","Blue (only while held)","Red (only while held)","Green (only while held)"]
global KEYMAPVALUES := ["cycle","blue","red","green","blue hold","red hold","green hold"]
global COL_SWITCH := "0x6A3FA0"
global KEYMAPFILE := CFGDIR "\keymap.txt"
; Code the remapper reports for the wheel click in Identify (it has no keyboard scancode).
global WHEELCODE := 0x1004
global curLayer := 1
global quickIsKeymap := false
global QUICKKEYS := ["F13","F14","F15","F16","F17","F18","F19","F20","F21","F22","F23","F24",
"Enter","Space","Esc","Tab","Backspace","Delete","Home","End","PgUp","PgDn",
"Up","Down","Left","Right","LCtrl","LShift","LAlt",
"Media_Play_Pause","Media_Next","Media_Prev","Volume_Up","Volume_Down","Volume_Mute",
"LButton","RButton","MButton","XButton1","XButton2"]
global ORDER := ["B01","B02","B03","B04","B05","B06","B07","B08","B09","B10","B11","B12","B13","B14",
"BESP","BALT","BUP","BDOWN","BLEFT","BRIGHT"]
global IDENTIFY_IDS := ORDER.Clone()
IDENTIFY_IDS.Push("BWHEEL")
global config := LoadConfig()
global origin := LoadOrigin()
global tile := Map()
global LAYOUT := Map(
"B01",[123,172], "B02",[142,166], "B03",[161,163], "B04",[179,163], "B05",[197,162],
"B06",[126,191], "B07",[144,185], "B08",[163,183], "B09",[180,182], "B10",[200,181],
"B11",[128,209], "B12",[147,203], "B13",[165,201], "B14",[183,200])
global SCALE := 3.1, ORIGINX := 115.0, ORIGINY := 152.0, SX := 40, SY := 86
; Default actions for keymap L (1 = Blue). Red and Green start with no changes.
Defaults(L := 1) {
d := Map()
for id in PHYS
d[id] := {type:"none", value:""}
if (L != 1)
return d
fk := ["F13","F14","F15","F16","F17","F18","F19","F20","F21","F22","F23","F24"]
i := 1
for id in ["B01","B02","B03","B04","B05","B06","B07","B08","B09","B10","B11","B12"] {
d[id] := {type:"key", value:fk[i]}
i++
}
d["B13"] := {type:"key", value:"Media_Play_Pause"}
d["B14"] := {type:"key", value:"Volume_Mute"}
return d
}
; config[L][id] = {type, value} for keymap L = 1 (Blue), 2 (Red), 3 (Green).
LoadConfig() {
c := [Defaults(1), Defaults(2), Defaults(3)]
if FileExist(INI) {
for L, sec in LAYERS
for id in PHYS {
raw := IniRead(INI, sec, id, "")
if raw != "" {
p := InStr(raw, "|")
c[L][id] := {type:(p ? SubStr(raw,1,p-1) : raw), value:(p ? SubStr(raw,p+1) : "")}
}
}
}
return c
}
SaveConfig() {
for L, sec in LAYERS
for id, def in config[L]
IniWrite(def.type "|" def.value, INI, sec, id)
}
LoadOrigin() {
o := Map()
if FileExist(INI)
for id in PHYS {
raw := IniRead(INI, "Origen", id, "")
if (raw != "" && IsXDigit(raw))
o[id] := Integer("0x" raw)
}
return o
}
SaveOrigin() {
try IniDelete(INI, "Origen")
for id, sc in origin
IniWrite(Format("{:02X}", sc), INI, "Origen", id)
}
DeviceIds() => StrSplit(IniRead(INI, "Dispositivo", "Ids", "VID_050D"), ",", " ")
IsN52te(hw) {
for id in DeviceIds()
if (id != "" && InStr(hw, id))
return true
return false
}
AddDeviceId(hw) {
if !RegExMatch(hw, "i)VID_[0-9A-F]{4}&PID_[0-9A-F]{4}", &m)
return ""
ids := DeviceIds()
ids.Push(StrUpper(m[0]))
s := ""
for id in ids
s .= (s = "" ? "" : ",") id
IniWrite(s, INI, "Dispositivo", "Ids")
return m[0]
}
ScToAhk(sc) => ((sc & 0xFF00) = 0xE000 ? 0x100 : 0) | (sc & 0xFF)
AhkToSc(sc) => (sc & 0x100) ? (0xE000 | (sc & 0xFF)) : sc
ScName(sc) {
if (sc = WHEELCODE)
return "Wheel click"
n := GetKeyName(Format("sc{:03X}", ScToAhk(sc)))
return n = "" ? Format("sc{:02X}", sc) : StrLen(n) = 1 ? StrUpper(n) : n
}
SourceSc(id) => (id = "BWHEEL") ? WHEELCODE : origin.Has(id) ? origin[id] : AhkToSc(GetKeySC(PHYS[id]))
SourceName(id) => origin.Has(id) ? ScName(origin[id]) : FACTORY[id]
if FileExist(A_ScriptDir "\infomalena.ico")
try TraySetIcon(A_ScriptDir "\infomalena.ico")
global g := Gui("-MaximizeBox", "n52te Editor")
g.BackColor := COL_BG
g.SetFont("s9", "Segoe UI")
g.OnEvent("Close", (*) => (StopLearn(), g.Hide()))
if FileExist(A_ScriptDir "\favicon.png")
g.AddPicture("x18 y10 w40 h40", A_ScriptDir "\favicon.png")
g.SetFont("s12 Bold cWhite")
g.AddText("x66 y10 w400", "n52te Button Editor")
g.SetFont("s8 cSilver")
g.AddText("x66 y34 w560", "Click a button, choose what it does and press Assign. Changes apply instantly.")
; Keymap tabs: which keymap the editor is showing and editing.
g.SetFont("s9 Bold cSilver")
global tabs := []
for i, nm in LAYERNAMES {
tabCtl := g.AddText("x" (20 + (i-1)*78) " y58 w74 h24 +Center +0x200 +Border Background" COL_NONE, nm)
tabCtl.OnEvent("Click", SetEditLayer.Bind(i))
tabs.Push(tabCtl)
}
g.SetFont("s8 Bold cSilver")
global txtActive := g.AddText("x262 y63 w250", "")
g.SetFont("s9 cWhite")
AddTile(id, x, y, w, h) {
t := g.AddText("x" Round(x) " y" Round(y) " w" w " h" h " +Center +Border", "")
t.OnEvent("Click", SelectPad.Bind(id))
tile[id] := t
}
PlaceMain(id, w, h) {
c := LAYOUT[id]
AddTile(id, SX + (c[1]-ORIGINX)*SCALE - w/2, SY + (c[2]-ORIGINY)*SCALE - h/2, w, h)
}
for id in ["B01","B02","B03","B04","B05","B06","B07","B08","B09","B10","B11","B12","B13","B14"]
PlaceMain(id, 54, 42)
g.SetFont("s8 cSilver")
g.AddText("x360 y96 w160", "Thumb buttons and D-pad")
g.SetFont("s9 cWhite")
AddTile("BALT",  360, 120, 120, 44)
AddTile("BUP",   404, 178, 56, 34)
AddTile("BLEFT", 360, 216, 56, 34)
AddTile("BRIGHT",448, 216, 56, 34)
AddTile("BDOWN", 404, 254, 56, 34)
AddTile("BESP",  360, 300, 120, 44)
AddTile("BWHEEL", 230, 296, 110, 44)
g.SetFont("s9 cWhite")
panelX := 520
g.AddText("x" panelX " y60 w300 h334 +Border Background" COL_PANEL)
g.SetFont("s9 Bold cWhite")
global txtSel := g.AddText("x" (panelX+14) " y74 w272", "Choose a button")
g.SetFont("s8 cSilver")
global txtFac := g.AddText("x" (panelX+14) " y96 w272", "")
g.SetFont("s9 cWhite")
global btnIdent := g.AddButton("x" (panelX+14) " y120 w134 h30", "Identify")
btnIdent.OnEvent("Click", StartIdentify)
global btnLearn := g.AddButton("x" (panelX+152) " y120 w134 h30", "Learn keys")
btnLearn.OnEvent("Click", StartWizard)
g.AddText("x" (panelX+14) " y160 w272 cWhite", "What the button does:")
global ddlType := g.AddDropDownList("x" (panelX+14) " y180 w272", TYPELABELS)
ddlType.OnEvent("Change", (*) => UpdateHint())
global lblQuick := g.AddText("x" (panelX+14) " y212 w80 cWhite", "Quick key:")
global ddlQuick := g.AddDropDownList("x" (panelX+100) " y208 w186", QUICKKEYS)
ddlQuick.OnEvent("Change", (*) => (edtVal.Value := quickIsKeymap ? KEYMAPVALUES[ddlQuick.Value] : ddlQuick.Text))
g.AddText("x" (panelX+14) " y240 w80 cWhite", "Value:")
global edtVal := g.AddEdit("x" (panelX+100) " y236 w186 cBlack")
g.SetFont("s8 cSilver")
global txtHint := g.AddText("x" (panelX+14) " y268 w272 h44 +Wrap", "")
g.SetFont("s10 Bold cWhite")
global btnAssign := g.AddButton("x" (panelX+14) " y316 w272 h40", "Assign to this button")
btnAssign.OnEvent("Click", AssignCurrent)
g.SetFont("s9 cWhite")
global btnDef := g.AddButton("x20 y406 w150 h28", "Restore defaults")
btnDef.OnEvent("Click", ResetDefaults)
global chkStart := g.AddCheckbox("x185 y411 w320 cWhite", "Start the remapper with Windows")
chkStart.OnEvent("Click", ToggleStartup)
g.SetFont("s8 cSilver")
global txtDev := g.AddText("x20 y362 w330 h40 +Wrap", "")
global txtStatus := g.AddText("x20 y444 w580 h36 +Wrap", "")
g.SetFont("s9 cWhite")
if FileExist(A_ScriptDir "\logo_footer.png")
g.AddPicture("x630 y426 w200 h38", A_ScriptDir "\logo_footer.png")
global CRIT_ID := (*) => identifyMode && WinActive("n52te Editor")
HotIf CRIT_ID
for tmpId, tmpKey in PHYS
Hotkey("*" tmpKey, IdentifyHit.Bind(tmpId), "On")
HotIf
StartIdentify(*) {
global identifyMode, learnMode
if (learnMode = "wizard") {
StopLearn()
SetStatus("Learning canceled. Nothing was changed.")
return
}
if !ProcessExist("n52te_interception.exe") {
identifyMode := true
SetStatus("IDENTIFY MODE: press a button on the n52te now...")
return
}
learnMode := "identify"
ArmLearn()
SetStatus("IDENTIFY MODE: press a button on the n52te now...")
}
IdentifyHit(id, *) {
global identifyMode := false
SoundBeep 700, 70
SelectPad(id)
}
StartWizard(*) {
global learnMode, wizIdx, wizNew
if (learnMode = "wizard") {
wizIdx++
WizardNext()
return
}
if !ProcessExist("n52te_interception.exe") {
SetStatus("The remapper must be running to learn keys.")
return
}
if MsgBox("You will press the n52te's buttons one at a time, in the order shown below."
. "`n`nThis way the editor learns which key each button sends with the profile stored on your keypad"
. " (red, green, blue...) and recognizes your device."
. "`n`nIf a button does nothing, press 'Skip'.", "n52te Editor", "OKCancel Iconi") != "OK"
return
learnMode := "wizard"
wizIdx := 1
wizNew := Map()
btnIdent.Text := "Cancel"
btnLearn.Text := "Skip"
WizardNext()
}
WizardNext() {
global wizIdx, origin, learnMode
if (wizIdx > ORDER.Length) {
StopLearn()
origin := wizNew
SaveOrigin()
RefreshAll()
SetStatus("Done: " wizNew.Count " buttons learned and applied. You can now assign actions to them.")
return
}
id := ORDER[wizIdx]
SelectPad(id)
ArmLearn()
SetStatus("LEARN (" wizIdx "/" ORDER.Length "): press button " NICE[id] " on the n52te (or 'Skip').")
}
ArmLearn() {
try FileDelete(LEARNRES)
try FileAppend("", LEARNREQ)
SetTimer(PollLearn, 100)
}
StopLearn() {
global learnMode := ""
SetTimer(PollLearn, 0)
try FileDelete(LEARNREQ)
try FileDelete(LEARNRES)
btnIdent.Text := "Identify"
btnLearn.Text := "Learn keys"
}
PollLearn() {
global wizIdx
if !FileExist(LEARNRES)
return
try {
lines := StrSplit(Trim(FileRead(LEARNRES, "UTF-8"), "`r`n"), "`n", "`r")
FileDelete(LEARNRES)
} catch
return
hw := lines.Length >= 1 ? Trim(lines[1]) : ""
sc := (lines.Length >= 2 && IsXDigit(Trim(lines[2]))) ? Integer("0x" Trim(lines[2])) : 0
if !sc {
ArmLearn()
return
}
if !IsN52te(hw) {
if (hw = "") {
SetStatus("Couldn't read that keyboard's ID. Press a button on the n52te.")
ArmLearn()
return
}
RegExMatch(hw, "i)VID_[0-9A-F]{4}&PID_[0-9A-F]{4}", &m)
if MsgBox("That key (" ScName(sc) ") comes from a device that isn't recognized as an n52te yet:`n`n"
. (m ? m[0] : hw) "`n`nIs this your n52te? (If you pressed it on your regular keyboard, answer No.)",
"n52te Editor", "YesNo Icon?") != "Yes" {
SetStatus("Ignored. Press the button on the n52te.")
ArmLearn()
return
}
if (AddDeviceId(hw) = "") {
SetStatus("Couldn't get that device's VID/PID. Please open an issue and include the text from the remapper window.")
StopLearn()
return
}
}
if (learnMode = "wizard" && sc = WHEELCODE) {
ArmLearn()
SetStatus("That was the wheel click, which isn't part of Learn keys. Press button " NICE[ORDER[wizIdx]] " or 'Skip'.")
return
}
SoundBeep 700, 70
if (learnMode = "identify") {
StopLearn()
for id in IDENTIFY_IDS
if (SourceSc(id) = sc) {
SelectPad(id)
return
}
SetStatus("That key (" ScName(sc) ") doesn't match any button. Use 'Learn keys' so the editor learns your keypad.")
return
}
for prevId, prevSc in wizNew
if (prevSc = sc) {
ArmLearn()
SetStatus("That key (" ScName(sc) ") is already sent by button " NICE[prevId] ". Press button " NICE[ORDER[wizIdx]] " or 'Skip'.")
return
}
wizNew[ORDER[wizIdx]] := sc
wizIdx++
WizardNext()
}
ColorOf(id) {
if id = selectedId
return COL_SEL
t := config[curLayer][id].type
return (t = "none") ? COL_NONE : (t = "keymap") ? COL_SWITCH : COL_ASSIGN
}
ShortVal(id) {
def := config[curLayer][id]
if def.type = "none"
return "—"
if def.type = "keymap" {
v := def.value
if (v = "cycle")
return "Cycle"
return InStr(v, " hold") ? "Hold " StrTitle(StrReplace(v, " hold")) : "→ " StrTitle(v)
}
if def.type = "text"
return "text"
if def.type = "run"
return "open"
if def.type = "send"
return def.value
return def.value
}
RefreshTile(id) {
t := tile[id]
t.Opt("+Background" ColorOf(id) " +Redraw")
t.Value := NICE[id] "  " SourceName(id) "`n" ShortVal(id)
}
RefreshAll() {
for id in PHYS
RefreshTile(id)
}
SelectPad(id, *) {
global selectedId := id
def := config[curLayer][id]
txtSel.Value := "Button " NICE[id] "  ·  " LAYERNAMES[curLayer] " keymap"
txtFac.Value := origin.Has(id) ? "Your keypad sends: " SourceName(id) : "Factory default: " FACTORY[id]
idx := 5
for i, t in TYPES
if (t = def.type)
idx := i
ddlType.Value := idx
edtVal.Value := def.value
ddlQuick.Value := 0
UpdateHint()
if (def.type = "keymap")
for i, v in KEYMAPVALUES
if (v = def.value)
ddlQuick.Value := i
RefreshAll()
SetStatus("Configuring button " NICE[id] " in the " LAYERNAMES[curLayer] " keymap. Choose an action and press Assign.")
}
; Shows keymap choices in the quick list for "Switch keymap", key names otherwise.
FillQuick(km) {
global quickIsKeymap
if (km = quickIsKeymap)
return
quickIsKeymap := km
ddlQuick.Delete()
ddlQuick.Add(km ? KEYMAPLABELS : QUICKKEYS)
lblQuick.Value := km ? "Keymap:" : "Quick key:"
}
UpdateHint() {
hints := [
"The key stays down while you hold the button (ideal for games). Pick from the list or type a key name (e.g. F14, Space, LButton).",
"Sends a key combination. E.g.  ^c = Ctrl+C   !{Tab} = Alt+Tab   {F5 2} = F5 twice.",
"Types the text in Value (phrases, chat commands...).",
"Opens whatever is in Value. E.g.  notepad.exe   |   https://google.com",
"The button keeps its factory behavior.",
"Switches the remapper's keymap; works the same in all three. The keypad's own light can't follow it: see 'Remapper keymap' top left."
]
i := ddlType.Value
FillQuick(i = 6)
txtHint.Value := (i >= 1 && i <= hints.Length) ? hints[i] : ""
}
AssignCurrent(*) {
if selectedId = "" {
SetStatus("First click a button in the drawing (or use Identify).")
return
}
t := TYPES[ddlType.Value ? ddlType.Value : 5]
v := Trim(edtVal.Value)
if (t != "none" && v = "") {
SetStatus("Enter a value, or choose 'No change'.")
return
}
if (t = "keymap") {
v := StrLower(v)
valid := false
for kv in KEYMAPVALUES
if (kv = v)
valid := true
if !valid {
SetStatus("For 'Switch keymap', pick an option from the Keymap list (e.g. cycle, red, red hold).")
return
}
; Like the original software, a keymap button does the same in all three keymaps.
for L in [1, 2, 3]
config[L][selectedId] := {type:t, value:v}
} else {
wasSwitch := config[curLayer][selectedId].type = "keymap"
config[curLayer][selectedId] := {type:t, value:v}
if wasSwitch
for L in [1, 2, 3]
if (L != curLayer && config[L][selectedId].type = "keymap")
config[L][selectedId] := {type:"none", value:""}
}
SaveConfig()
RefreshAll()
if (t = "keymap")
SetStatus("OK: button " NICE[selectedId] " now switches keymaps (" v "), in all three keymaps. Test it outside this window.")
else
SetStatus("OK: button " NICE[selectedId] " assigned in the " LAYERNAMES[curLayer] " keymap. To TEST it, click outside this window and press the button on the n52te.")
}
ResetDefaults(*) {
if MsgBox("Restore ALL buttons in all three keymaps (Blue, Red, Green) to their defaults?", "n52te Editor", "YesNo Icon?") = "Yes" {
global config := [Defaults(1), Defaults(2), Defaults(3)]
SaveConfig()
RefreshAll()
SetStatus("Defaults restored and applied.")
}
}
; Switches which keymap the editor shows and edits (not the one the remapper is using).
SetEditLayer(i, *) {
global curLayer := i
for j, tabCtl in tabs {
tabCtl.Opt("+Background" (j = i ? LAYERBG[j] : COL_NONE))
tabCtl.SetFont(j = i ? "cWhite" : "cSilver")
tabCtl.Redraw()
}
RefreshAll()
if (selectedId != "")
SelectPad(selectedId)
}
; The keypad's lights can't be changed without Belkin's driver, so show the keymap
; the remapper is using here instead (it writes it to keymap.txt).
RefreshActive() {
static last := "-"
k := ""
if ProcessExist("n52te_interception.exe")
try k := Trim(FileRead(KEYMAPFILE, "UTF-8"), " `t`r`n")
if (k = last)
return
last := k
idx := 0
for i, nm in LAYERNAMES
if (nm = k)
idx := i
txtActive.SetFont("c" (idx ? LAYERFG[idx] : "Silver"))
txtActive.Value := idx ? "Remapper keymap: " k " (not the light)" : ""
}
SetStatus(msg) {
on := ProcessExist("n52te_interception.exe")
txtStatus.Value := msg "`n" (on ? "Remapper: RUNNING (changes apply automatically)." : "WARNING: the remapper isn't running. Start it from the 'n52te Remapper' shortcut.")
RefreshDevice(on)
}
RefreshDevice(on) {
if (!on || !FileExist(KBDLIST)) {
txtDev.Value := ""
return
}
try txt := FileRead(KBDLIST, "UTF-8")
catch
return
if RegExMatch(txt, "m)^\[n52te\].*?(VID_[0-9A-F]{4}&PID_[0-9A-F]{4})", &m)
txtDev.Value := "n52te detected by the remapper (" StrReplace(m[1], "&", "&&") ")."
else
txtDev.Value := "The remapper does NOT recognize your n52te, so nothing changes. Press 'Learn keys' so it can recognize it."
}
global RUNKEY := "HKCU\Software\Microsoft\Windows\CurrentVersion\Run"
StartupOn() {
try {
v := RegRead(RUNKEY, "n52te")
return v != ""
} catch {
return false
}
}
ToggleStartup(*) {
if chkStart.Value {
RegWrite('"' A_ScriptDir '\n52te_interception.exe"', "REG_SZ", RUNKEY, "n52te")
SetStatus("Autostart ON: the remapper will start automatically with Windows.")
} else {
try RegDelete(RUNKEY, "n52te")
SetStatus("Autostart OFF.")
}
}
A_TrayMenu.Delete()
A_TrayMenu.Add("Open editor", (*) => g.Show())
A_TrayMenu.Add("Exit", (*) => ExitApp())
A_TrayMenu.Default := "Open editor"
A_IconTip := "n52te Editor"
SetEditLayer(1)
RefreshActive()
SetTimer(RefreshActive, 500)
chkStart.Value := StartupOn() ? 1 : 0
SetStatus("Ready. Click a button to configure it.")
g.Show("w840 h488")
SetWinIcon()
SetWinIcon() {
ico := A_ScriptDir "\infomalena.ico"
if !FileExist(ico)
return
big   := DllCall("LoadImage", "ptr",0, "str",ico, "uint",1, "int",32, "int",32, "uint",0x10, "ptr")
small := DllCall("LoadImage", "ptr",0, "str",ico, "uint",1, "int",16, "int",16, "uint",0x10, "ptr")
if big
SendMessage(0x80, 1, big, , "ahk_id " g.Hwnd)
if small
SendMessage(0x80, 0, small, , "ahk_id " g.Hwnd)
}
