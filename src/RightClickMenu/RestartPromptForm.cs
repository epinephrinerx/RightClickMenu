namespace RightClickMenu;

public sealed class RestartPromptForm : Form
{
    public const string PromptText = "ต้องเริ่ม Explorer ใหม่เพื่อให้สไตล์เมนูคลิกขวาใหม่มีผล\n\nคำเตือน: การเริ่มใหม่จะปิดหน้าต่างบางหน้าต่าง เช่น หน้าต่าง File Explorer ที่เปิดอยู่";

    public RestartPromptForm()
    {
        Text = "เริ่ม Explorer ใหม่";
        ClientSize = new Size(500, 215);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ControlBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        var message = new Label
        {
            Text = PromptText, Location = new Point(20, 20), Size = new Size(460, 125)
        };
        var restart = new Button
        {
            Text = "เริ่มใหม่", DialogResult = DialogResult.OK,
            Location = new Point(260, 160), Size = new Size(100, 35), TabIndex = 1
        };
        var cancel = new Button
        {
            Text = "ยกเลิก", DialogResult = DialogResult.Cancel,
            Location = new Point(380, 160), Size = new Size(100, 35), TabIndex = 0
        };
        Controls.AddRange([message, restart, cancel]);
        AcceptButton = cancel;
        CancelButton = cancel;
        ActiveControl = cancel;
    }
}
