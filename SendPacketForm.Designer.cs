//NCShark - By AlSch092 @ Github, thanks to @Diamondo25 for MapleShark
namespace MapleShark
{
    partial class SendPacketForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.textBox_Send = new System.Windows.Forms.TextBox();
            this.button_SendPacket = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // textBox_Send
            // 
            this.textBox_Send.Location = new System.Drawing.Point(12, 12);
            this.textBox_Send.Name = "textBox_Send";
            this.textBox_Send.Size = new System.Drawing.Size(403, 20);
            this.textBox_Send.TabIndex = 14;
            // 
            // button_SendPacket
            // 
            this.button_SendPacket.Location = new System.Drawing.Point(421, 10);
            this.button_SendPacket.Name = "button_SendPacket";
            this.button_SendPacket.Size = new System.Drawing.Size(75, 23);
            this.button_SendPacket.TabIndex = 15;
            this.button_SendPacket.Text = "Send";
            this.button_SendPacket.UseVisualStyleBackColor = true;
            this.button_SendPacket.Click += new System.EventHandler(this.button_SendPacket_Click);
            // 
            // SendPacketForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(508, 39);
            this.Controls.Add(this.button_SendPacket);
            this.Controls.Add(this.textBox_Send);
            this.Name = "SendPacketForm";
            this.ShowIcon = false;
            this.Text = "Send Data";
            this.Load += new System.EventHandler(this.SendPacketForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textBox_Send;
        private System.Windows.Forms.Button button_SendPacket;
    }
}