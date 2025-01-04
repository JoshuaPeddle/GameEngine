namespace GameEngine
{
    partial class MainView
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
            skglControl1 = new SkiaSharp.Views.Desktop.SKGLControl();
            SuspendLayout();
            // 
            // skglControl1
            // 
            skglControl1.BackColor = Color.Black;
            skglControl1.Location = new Point(6, 7);
            skglControl1.Margin = new Padding(6, 7, 6, 7);
            skglControl1.Name = "skglControl1";
            skglControl1.Size = new Size(1174, 678);
            skglControl1.TabIndex = 0;
            skglControl1.VSync = false;
            // 
            // MainView
            // 
            AutoScaleDimensions = new SizeF(13F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1185, 685);
            Controls.Add(skglControl1);
            Name = "MainView";
            Text = "Form1";
            ResumeLayout(false);
        }

        #endregion

        private SkiaSharp.Views.Desktop.SKGLControl skglControl1;
    }
}
