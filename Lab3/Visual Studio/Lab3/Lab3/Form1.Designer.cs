namespace Lab3
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            comboPorts = new ComboBox();
            btnConnect = new Button();
            btnOn = new Button();
            btnOff = new Button();
            lblStatus = new Label();
            SuspendLayout();
            // 
            // comboPorts
            // 
            comboPorts.FormattingEnabled = true;
            comboPorts.Location = new Point(898, 237);
            comboPorts.Name = "comboPorts";
            comboPorts.Size = new Size(182, 31);
            comboPorts.TabIndex = 0;
            // 
            // btnConnect
            // 
            btnConnect.Location = new Point(568, 369);
            btnConnect.Name = "btnConnect";
            btnConnect.Size = new Size(286, 103);
            btnConnect.TabIndex = 1;
            btnConnect.Text = "\t連線";
            btnConnect.UseVisualStyleBackColor = true;
            // 
            // btnOn
            // 
            btnOn.Location = new Point(873, 369);
            btnOn.Name = "btnOn";
            btnOn.Size = new Size(264, 103);
            btnOn.TabIndex = 2;
            btnOn.Text = "On";
            btnOn.UseVisualStyleBackColor = true;
            // 
            // btnOff
            // 
            btnOff.Location = new Point(1155, 369);
            btnOff.Name = "btnOff";
            btnOff.Size = new Size(235, 103);
            btnOff.TabIndex = 3;
            btnOff.Text = "Off";
            btnOff.UseVisualStyleBackColor = true;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(1086, 240);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(46, 23);
            lblStatus.TabIndex = 4;
            lblStatus.Text = "狀態";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(11F, 23F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1941, 793);
            Controls.Add(lblStatus);
            Controls.Add(btnOff);
            Controls.Add(btnOn);
            Controls.Add(btnConnect);
            Controls.Add(comboPorts);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ComboBox comboPorts;
        private Button btnConnect;
        private Button btnOn;
        private Button btnOff;
        private Label lblStatus;
    }
}
