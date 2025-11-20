
namespace TarkovPriceViewer.UI
{
    partial class KeyPressCheck
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(KeyPressCheck));
			label1 = new System.Windows.Forms.Label();
			label2 = new System.Windows.Forms.Label();
			SuspendLayout();
			// 
			// label1
			// 
			label1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			label1.AutoSize = true;
			label1.Location = new System.Drawing.Point(31, 56);
			label1.Name = "label1";
			label1.RightToLeft = System.Windows.Forms.RightToLeft.No;
			label1.Size = new System.Drawing.Size(56, 15);
			label1.TabIndex = 0;
			label1.Text = "Press Key";
			// 
			// label2
			// 
			label2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			label2.AutoSize = true;
			label2.Font = new System.Drawing.Font("Segoe UI", 7F);
			label2.Location = new System.Drawing.Point(19, 100);
			label2.Name = "label2";
			label2.RightToLeft = System.Windows.Forms.RightToLeft.No;
			label2.Size = new System.Drawing.Size(83, 12);
			label2.TabIndex = 1;
			label2.Text = "Press ESC to close";
			// 
			// KeyPressCheck
			// 
			AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
			AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
			ClientSize = new System.Drawing.Size(120, 121);
			ControlBox = false;
			Controls.Add(label2);
			Controls.Add(label1);
			FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
			Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
			Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			Name = "KeyPressCheck";
			StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			FormClosed += KeyPressCheck_FormClosed;
			KeyUp += KeyPressCheck_KeyUp;
			ResumeLayout(false);
			PerformLayout();
		}

		#endregion

		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label label2;
	}
}