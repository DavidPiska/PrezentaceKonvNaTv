/*
 * Created by SharpDevelop.
 * User: DavidPiska
 * Date: 5.10.2017
 * Time: 14:15
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Windows.Forms;

namespace PrezentaceBlue
{
	public static class Prompt
	{
	    public static string ShowDialog(string text, string caption, string ss)
	    {
	        Form prompt = new Form()
	        {
	            Width = 500,
	            Height = 150,
	            FormBorderStyle = FormBorderStyle.FixedDialog,
	            Text = caption,
	            StartPosition = FormStartPosition.CenterScreen
	        };
	        Label textLabel = new Label() { Left = 50, Top=20, Text=text };
	        TextBox textBox = new TextBox() { Left = 50, Top=50, Width=400, Text=ss };
	        Button confirmation = new Button() { Text = "Ok", Left=350, Width=100, Top=70, DialogResult = DialogResult.OK };
	        confirmation.Click += (sender, e) => { prompt.Close(); };
	        prompt.Controls.Add(textBox);
	        prompt.Controls.Add(confirmation);
	        prompt.Controls.Add(textLabel);
	        prompt.AcceptButton = confirmation;
	        prompt.BringToFront();
	
	        return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
	    }
	}
}
