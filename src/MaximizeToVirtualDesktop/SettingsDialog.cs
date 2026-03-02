using MaximizeToVirtualDesktop.Interop;

namespace MaximizeToVirtualDesktop;

/// <summary>
/// Modal settings dialog for configuring hotkeys and maximize-button behavior.
/// Call <see cref="ApplyToSettings"/> after <c>ShowDialog</c> returns <c>DialogResult.OK</c>.
/// </summary>
internal sealed class SettingsDialog : Form
{
    private readonly AppSettings _settings;

    private readonly CheckBox _chkHotkeyCtrl, _chkHotkeyAlt, _chkHotkeyShift, _chkHotkeyWin;
    private readonly ComboBox _cmbHotkeyKey;

    private readonly CheckBox _chkPinCtrl, _chkPinAlt, _chkPinShift, _chkPinWin;
    private readonly ComboBox _cmbPinKey;

    private readonly CheckBox _chkInvertShiftClick;

    // (display name, VK code) pairs for the key combo boxes
    private static readonly (string Name, uint Vk)[] SupportedKeys =
    [
        .. Enumerable.Range('A', 26).Select(c => (((char)c).ToString(), (uint)c)),
        .. Enumerable.Range(1, 12).Select(i => ($"F{i}", (uint)(0x70 + i - 1))),
    ];

    public SettingsDialog(AppSettings settings)
    {
        _settings = settings;

        Text = "Settings — Maximize to Virtual Desktop";
        ClientSize = new Size(420, 310);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        int y = 10;

        // Maximize hotkey group
        var grpMaximize = new GroupBox { Text = "Maximize Hotkey", Location = new Point(12, y), Size = new Size(396, 64) };
        Controls.Add(grpMaximize);
        (_chkHotkeyCtrl, _chkHotkeyAlt, _chkHotkeyShift, _chkHotkeyWin, _cmbHotkeyKey) =
            AddHotkeyRow(grpMaximize, settings.HotkeyModifiers, settings.HotkeyKey);
        y += 72;

        // Pin hotkey group
        var grpPin = new GroupBox { Text = "Pin Hotkey", Location = new Point(12, y), Size = new Size(396, 64) };
        Controls.Add(grpPin);
        (_chkPinCtrl, _chkPinAlt, _chkPinShift, _chkPinWin, _cmbPinKey) =
            AddHotkeyRow(grpPin, settings.PinHotkeyModifiers, settings.PinHotkeyKey);
        y += 72;

        // Behavior group
        var grpBehavior = new GroupBox { Text = "Behavior", Location = new Point(12, y), Size = new Size(396, 72) };
        Controls.Add(grpBehavior);
        _chkInvertShiftClick = new CheckBox
        {
            Text = "Always maximize to virtual desktop on maximize button click\r\n" +
                   "(Shift+Click performs a normal maximize instead)",
            Location = new Point(12, 20),
            Size = new Size(372, 42),
            Checked = settings.InvertShiftClick,
        };
        grpBehavior.Controls.Add(_chkInvertShiftClick);
        y += 80;

        // Buttons
        y += 6;
        var btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(12, y),
            Size = new Size(80, 28),
            FlatStyle = FlatStyle.System,
        };
        var btnCancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(100, y),
            Size = new Size(80, 28),
            FlatStyle = FlatStyle.System,
        };
        var btnReset = new Button
        {
            Text = "Reset Defaults",
            Location = new Point(192, y),
            Size = new Size(110, 28),
            FlatStyle = FlatStyle.System,
        };
        btnReset.Click += (_, _) => ResetDefaults();

        Controls.AddRange(new Control[] { btnOk, btnCancel, btnReset });
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        ClientSize = new Size(420, y + 44);
    }

    private static (CheckBox ctrl, CheckBox alt, CheckBox shift, CheckBox win, ComboBox key)
        AddHotkeyRow(GroupBox grp, uint modifiers, uint vk)
    {
        var chkCtrl  = new CheckBox { Text = "Ctrl",  Checked = (modifiers & NativeMethods.MOD_CONTROL) != 0, AutoSize = true, Location = new Point(12,  28) };
        var chkAlt   = new CheckBox { Text = "Alt",   Checked = (modifiers & NativeMethods.MOD_ALT)     != 0, AutoSize = true, Location = new Point(68,  28) };
        var chkShift = new CheckBox { Text = "Shift", Checked = (modifiers & NativeMethods.MOD_SHIFT)   != 0, AutoSize = true, Location = new Point(118, 28) };
        var chkWin   = new CheckBox { Text = "Win",   Checked = (modifiers & NativeMethods.MOD_WIN)     != 0, AutoSize = true, Location = new Point(182, 28) };
        var lblKey   = new Label    { Text = "Key:",  AutoSize = true, Location = new Point(244, 30) };
        var cmb = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(278, 26),
            Size = new Size(72, 24),
        };

        foreach (var (name, _) in SupportedKeys)
            cmb.Items.Add(name);

        var idx = Array.FindIndex(SupportedKeys, k => k.Vk == vk);
        cmb.SelectedIndex = Math.Max(0, idx);

        grp.Controls.AddRange(new Control[] { chkCtrl, chkAlt, chkShift, chkWin, lblKey, cmb });
        return (chkCtrl, chkAlt, chkShift, chkWin, cmb);
    }

    private void ResetDefaults()
    {
        _chkHotkeyCtrl.Checked  = true;
        _chkHotkeyAlt.Checked   = true;
        _chkHotkeyShift.Checked = true;
        _chkHotkeyWin.Checked   = false;
        _cmbHotkeyKey.SelectedIndex = Array.FindIndex(SupportedKeys, k => k.Vk == NativeMethods.VK_X);

        _chkPinCtrl.Checked  = true;
        _chkPinAlt.Checked   = true;
        _chkPinShift.Checked = true;
        _chkPinWin.Checked   = false;
        _cmbPinKey.SelectedIndex = Array.FindIndex(SupportedKeys, k => k.Vk == NativeMethods.VK_P);

        _chkInvertShiftClick.Checked = false;
    }

    /// <summary>Writes the dialog's current values back into the <see cref="AppSettings"/> object.</summary>
    public void ApplyToSettings()
    {
        _settings.HotkeyModifiers    = BuildModifiers(_chkHotkeyCtrl, _chkHotkeyAlt, _chkHotkeyShift, _chkHotkeyWin);
        _settings.HotkeyKey          = _cmbHotkeyKey.SelectedIndex >= 0 ? SupportedKeys[_cmbHotkeyKey.SelectedIndex].Vk : NativeMethods.VK_X;
        _settings.PinHotkeyModifiers = BuildModifiers(_chkPinCtrl, _chkPinAlt, _chkPinShift, _chkPinWin);
        _settings.PinHotkeyKey       = _cmbPinKey.SelectedIndex >= 0 ? SupportedKeys[_cmbPinKey.SelectedIndex].Vk : NativeMethods.VK_P;
        _settings.InvertShiftClick   = _chkInvertShiftClick.Checked;
    }

    private static uint BuildModifiers(CheckBox ctrl, CheckBox alt, CheckBox shift, CheckBox win)
    {
        uint m = 0;
        if (ctrl.Checked)  m |= NativeMethods.MOD_CONTROL;
        if (alt.Checked)   m |= NativeMethods.MOD_ALT;
        if (shift.Checked) m |= NativeMethods.MOD_SHIFT;
        if (win.Checked)   m |= NativeMethods.MOD_WIN;
        return m;
    }
}
