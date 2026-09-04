# StandUpReminder.ps1
# A lightweight system-tray reminder that tells you to stand up on an interval.
# Reminder styles: full-screen Flash, tray Notification, or Both (user choice).
# No installation or external modules required - pure Windows PowerShell + WinForms.

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# ---------------------------------------------------------------------------
# Settings (persisted next to this script as settings.json)
# ---------------------------------------------------------------------------
$script:SettingsPath = Join-Path $PSScriptRoot 'settings.json'

$script:Defaults = [ordered]@{
    IntervalMinutes = 30
    Mode            = 'Both'   # 'Notification' | 'Flash' | 'Both'
    FlashSeconds    = 15       # how long the flash stays before auto-closing
    PlaySound       = $true
    Message         = 'Time to STAND UP and stretch!'
}

function Load-Settings {
    if (Test-Path $script:SettingsPath) {
        try {
            $loaded = Get-Content $script:SettingsPath -Raw | ConvertFrom-Json
            $s = @{}
            foreach ($k in $script:Defaults.Keys) {
                if ($null -ne $loaded.$k) { $s[$k] = $loaded.$k } else { $s[$k] = $script:Defaults[$k] }
            }
            return $s
        } catch { }
    }
    $s = @{}
    foreach ($k in $script:Defaults.Keys) { $s[$k] = $script:Defaults[$k] }
    return $s
}

function Save-Settings($s) {
    try {
        [PSCustomObject]$s | ConvertTo-Json | Set-Content -Path $script:SettingsPath -Encoding UTF8
    } catch { }
}

$script:Settings = Load-Settings

# ---------------------------------------------------------------------------
# The full-screen "flash" reminder
# ---------------------------------------------------------------------------
function Show-Flash {
    $screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds

    $form = New-Object System.Windows.Forms.Form
    $form.FormBorderStyle = 'None'
    $form.StartPosition   = 'Manual'
    $form.Bounds          = $screen
    $form.TopMost         = $true
    $form.BackColor       = [System.Drawing.Color]::FromArgb(20, 20, 40)
    $form.ShowInTaskbar   = $false

    $label = New-Object System.Windows.Forms.Label
    $label.Text      = $script:Settings.Message
    $label.Font      = New-Object System.Drawing.Font('Segoe UI', 48, [System.Drawing.FontStyle]::Bold)
    $label.ForeColor = [System.Drawing.Color]::White
    $label.TextAlign = 'MiddleCenter'
    $label.Dock      = 'Fill'
    $form.Controls.Add($label)

    $hint = New-Object System.Windows.Forms.Label
    $hint.Text      = 'Click anywhere or press any key to dismiss'
    $hint.Font      = New-Object System.Drawing.Font('Segoe UI', 14)
    $hint.ForeColor = [System.Drawing.Color]::FromArgb(180, 200, 220, 255)
    $hint.TextAlign = 'MiddleCenter'
    $hint.Dock      = 'Bottom'
    $hint.Height    = 60
    $form.Controls.Add($hint)

    # Pulse the background between two colors for attention.
    $colors = @(
        [System.Drawing.Color]::FromArgb(20, 20, 40),
        [System.Drawing.Color]::FromArgb(200, 40, 60)
    )
    $script:pulseIndex = 0
    $pulse = New-Object System.Windows.Forms.Timer
    $pulse.Interval = 600
    $pulse.Add_Tick({
        $script:pulseIndex = ($script:pulseIndex + 1) % 2
        $form.BackColor = $colors[$script:pulseIndex]
    })

    # Auto-close countdown.
    $remaining = [int]$script:Settings.FlashSeconds
    $closer = New-Object System.Windows.Forms.Timer
    $closer.Interval = 1000
    $closer.Add_Tick({
        $script:flashRemaining--
        if ($script:flashRemaining -le 0) { $form.Close() }
    })
    $script:flashRemaining = $remaining

    $dismiss = { $pulse.Stop(); $closer.Stop(); $form.Close() }
    $form.Add_KeyDown($dismiss)
    $form.Add_Click($dismiss)
    $label.Add_Click($dismiss)
    $hint.Add_Click($dismiss)
    $form.KeyPreview = $true

    $form.Add_Shown({
        $form.Activate()
        $pulse.Start()
        $closer.Start()
        if ($script:Settings.PlaySound) { [System.Media.SystemSounds]::Exclamation.Play() }
    })

    [void]$form.ShowDialog()
    $pulse.Dispose(); $closer.Dispose(); $form.Dispose()
}

function Show-Notification {
    $script:NotifyIcon.BalloonTipTitle = 'Stand Up Reminder'
    $script:NotifyIcon.BalloonTipText  = $script:Settings.Message
    $script:NotifyIcon.BalloonTipIcon  = 'Info'
    $script:NotifyIcon.ShowBalloonTip(8000)
    if ($script:Settings.PlaySound) { [System.Media.SystemSounds]::Asterisk.Play() }
}

function Fire-Reminder {
    switch ($script:Settings.Mode) {
        'Notification' { Show-Notification }
        'Flash'        { Show-Flash }
        default        { Show-Notification; Show-Flash }  # 'Both'
    }
}

# ---------------------------------------------------------------------------
# Settings dialog
# ---------------------------------------------------------------------------
function Show-SettingsDialog {
    $dlg = New-Object System.Windows.Forms.Form
    $dlg.Text          = 'Stand Up Reminder - Settings'
    $dlg.Size          = New-Object System.Drawing.Size(380, 320)
    $dlg.StartPosition = 'CenterScreen'
    $dlg.FormBorderStyle = 'FixedDialog'
    $dlg.MaximizeBox   = $false
    $dlg.MinimizeBox   = $false

    $lblInt = New-Object System.Windows.Forms.Label
    $lblInt.Text = 'Remind me every (minutes):'
    $lblInt.Location = New-Object System.Drawing.Point(20, 20)
    $lblInt.AutoSize = $true
    $dlg.Controls.Add($lblInt)

    $numInt = New-Object System.Windows.Forms.NumericUpDown
    $numInt.Minimum = 1; $numInt.Maximum = 480
    $numInt.Value = [int]$script:Settings.IntervalMinutes
    $numInt.Location = New-Object System.Drawing.Point(230, 18)
    $numInt.Width = 100
    $dlg.Controls.Add($numInt)

    $lblMode = New-Object System.Windows.Forms.Label
    $lblMode.Text = 'Reminder style:'
    $lblMode.Location = New-Object System.Drawing.Point(20, 60)
    $lblMode.AutoSize = $true
    $dlg.Controls.Add($lblMode)

    $cmbMode = New-Object System.Windows.Forms.ComboBox
    $cmbMode.DropDownStyle = 'DropDownList'
    [void]$cmbMode.Items.AddRange(@('Notification', 'Flash', 'Both'))
    $cmbMode.SelectedItem = $script:Settings.Mode
    $cmbMode.Location = New-Object System.Drawing.Point(230, 58)
    $cmbMode.Width = 100
    $dlg.Controls.Add($cmbMode)

    $lblFlash = New-Object System.Windows.Forms.Label
    $lblFlash.Text = 'Flash duration (seconds):'
    $lblFlash.Location = New-Object System.Drawing.Point(20, 100)
    $lblFlash.AutoSize = $true
    $dlg.Controls.Add($lblFlash)

    $numFlash = New-Object System.Windows.Forms.NumericUpDown
    $numFlash.Minimum = 3; $numFlash.Maximum = 120
    $numFlash.Value = [int]$script:Settings.FlashSeconds
    $numFlash.Location = New-Object System.Drawing.Point(230, 98)
    $numFlash.Width = 100
    $dlg.Controls.Add($numFlash)

    $chkSound = New-Object System.Windows.Forms.CheckBox
    $chkSound.Text = 'Play a sound'
    $chkSound.Checked = [bool]$script:Settings.PlaySound
    $chkSound.Location = New-Object System.Drawing.Point(20, 138)
    $chkSound.AutoSize = $true
    $dlg.Controls.Add($chkSound)

    $lblMsg = New-Object System.Windows.Forms.Label
    $lblMsg.Text = 'Message:'
    $lblMsg.Location = New-Object System.Drawing.Point(20, 170)
    $lblMsg.AutoSize = $true
    $dlg.Controls.Add($lblMsg)

    $txtMsg = New-Object System.Windows.Forms.TextBox
    $txtMsg.Text = $script:Settings.Message
    $txtMsg.Location = New-Object System.Drawing.Point(20, 192)
    $txtMsg.Width = 310
    $dlg.Controls.Add($txtMsg)

    $btnOK = New-Object System.Windows.Forms.Button
    $btnOK.Text = 'Save'
    $btnOK.Location = New-Object System.Drawing.Point(150, 235)
    $btnOK.DialogResult = 'OK'
    $dlg.Controls.Add($btnOK)
    $dlg.AcceptButton = $btnOK

    $btnCancel = New-Object System.Windows.Forms.Button
    $btnCancel.Text = 'Cancel'
    $btnCancel.Location = New-Object System.Drawing.Point(250, 235)
    $btnCancel.DialogResult = 'Cancel'
    $dlg.Controls.Add($btnCancel)
    $dlg.CancelButton = $btnCancel

    if ($dlg.ShowDialog() -eq 'OK') {
        $script:Settings.IntervalMinutes = [int]$numInt.Value
        $script:Settings.Mode            = [string]$cmbMode.SelectedItem
        $script:Settings.FlashSeconds    = [int]$numFlash.Value
        $script:Settings.PlaySound       = [bool]$chkSound.Checked
        $script:Settings.Message         = [string]$txtMsg.Text
        Save-Settings $script:Settings
        Restart-Timer
        $script:NotifyIcon.Text = "Stand Up Reminder - every $($script:Settings.IntervalMinutes) min"
    }
    $dlg.Dispose()
}

# ---------------------------------------------------------------------------
# Interval timer
# ---------------------------------------------------------------------------
$script:MainTimer = New-Object System.Windows.Forms.Timer
$script:MainTimer.Add_Tick({ Fire-Reminder })

function Restart-Timer {
    $script:MainTimer.Stop()
    $script:MainTimer.Interval = [int]$script:Settings.IntervalMinutes * 60 * 1000
    $script:MainTimer.Start()
}

# ---------------------------------------------------------------------------
# Tray icon + menu
# ---------------------------------------------------------------------------
$script:NotifyIcon = New-Object System.Windows.Forms.NotifyIcon
$script:NotifyIcon.Icon = [System.Drawing.SystemIcons]::Information
$script:NotifyIcon.Text = "Stand Up Reminder - every $($script:Settings.IntervalMinutes) min"
$script:NotifyIcon.Visible = $true

$menu = New-Object System.Windows.Forms.ContextMenuStrip

$miStatus = $menu.Items.Add('Reminders: ON')
$miStatus.Enabled = $false

$miTest = $menu.Items.Add('Test reminder now')
$miTest.Add_Click({ Fire-Reminder })

$miPause = $menu.Items.Add('Pause')
$script:Paused = $false
$miPause.Add_Click({
    if ($script:Paused) {
        Restart-Timer
        $script:Paused = $false
        $miPause.Text = 'Pause'
        $miStatus.Text = 'Reminders: ON'
    } else {
        $script:MainTimer.Stop()
        $script:Paused = $true
        $miPause.Text = 'Resume'
        $miStatus.Text = 'Reminders: PAUSED'
    }
})

$miSettings = $menu.Items.Add('Settings...')
$miSettings.Add_Click({ Show-SettingsDialog })

[void]$menu.Items.Add('-')

$miExit = $menu.Items.Add('Exit')
$miExit.Add_Click({
    $script:MainTimer.Stop()
    $script:NotifyIcon.Visible = $false
    $script:NotifyIcon.Dispose()
    [System.Windows.Forms.Application]::Exit()
})

$script:NotifyIcon.ContextMenuStrip = $menu
$script:NotifyIcon.Add_MouseDoubleClick({ Show-SettingsDialog })

# ---------------------------------------------------------------------------
# Start
# ---------------------------------------------------------------------------
Restart-Timer
$script:NotifyIcon.ShowBalloonTip(4000, 'Stand Up Reminder',
    "Running in the tray. You'll be reminded every $($script:Settings.IntervalMinutes) minutes.", 'Info')

# Hidden form to keep the message loop alive.
$hidden = New-Object System.Windows.Forms.Form
$hidden.ShowInTaskbar = $false
$hidden.WindowState   = 'Minimized'
$hidden.FormBorderStyle = 'FixedToolWindow'
$hidden.Load += { $hidden.Hide() }
[System.Windows.Forms.Application]::Run($hidden)
