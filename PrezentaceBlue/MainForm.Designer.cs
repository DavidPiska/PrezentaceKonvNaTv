/*
 * Created by SharpDevelop.
 * User: DavidPiska
 * Date: 5.10.2017
 * Time: 9:21
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
namespace PrezentaceBlue
{
  partial class MainForm
  {
    /// <summary>
    /// Designer variable used to keep track of non-visual components.
    /// </summary>
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.PictureBox pictureBox1;
    private System.Windows.Forms.Label label1;

    /// <summary>
    /// Disposes resources used by the form.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        // Do NOT assign to Disposing (it's read-only) and do NOT manage threads here.
        if (components != null)
        {
          components.Dispose();
        }
      }
      base.Dispose(disposing);
    }

    /// <summary>
    /// This method is required for Windows Forms designer support.
    /// Do not change the method contents inside the source code editor. 
    /// The Forms designer might not be able to load this method if it was changed manually.
    /// </summary>
    private void InitializeComponent()
    {
      this.components = new System.ComponentModel.Container();
      this.pictureBox1 = new System.Windows.Forms.PictureBox();
      this.label1 = new System.Windows.Forms.Label();
      ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
      this.SuspendLayout();
      // 
      // pictureBox1
      // 
      this.pictureBox1.Location = new System.Drawing.Point(57, 18);
      this.pictureBox1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
      this.pictureBox1.Name = "pictureBox1";
      this.pictureBox1.Size = new System.Drawing.Size(520, 580);
      this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
      this.pictureBox1.TabIndex = 0;
      this.pictureBox1.TabStop = false;
      // 
      // label1
      // 
      this.label1.BackColor = System.Drawing.Color.White;
      this.label1.Location = new System.Drawing.Point(266, 395);
      this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
      this.label1.Name = "label1";
      this.label1.Size = new System.Drawing.Size(387, 334);
      this.label1.TabIndex = 1;
      this.label1.Text = "label1";
      // 
      // MainForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.BackColor = System.Drawing.Color.Black;
      this.ClientSize = new System.Drawing.Size(670, 743);
      this.Controls.Add(this.label1);
      this.Controls.Add(this.pictureBox1);
      this.DoubleBuffered = true;
      this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
      this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
      this.Name = "MainForm";
      this.Text = "PrezentaceBlue";
      // KeyDown / Timers handlers removed – worker thread handles all logic now.
      ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
      this.ResumeLayout(false);
    }
  }
}
