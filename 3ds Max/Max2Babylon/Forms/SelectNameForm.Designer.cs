﻿namespace Max2Babylon.Forms
{
    partial class SelectNameForm
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
            this.preDefiniedNameList = new System.Windows.Forms.TreeView();
            this.SuspendLayout();
            // 
            // preDefiniedNameList
            // 
            this.preDefiniedNameList.Location = new System.Drawing.Point(3, 2);
            this.preDefiniedNameList.Name = "preDefiniedNameList";
            this.preDefiniedNameList.Size = new System.Drawing.Size(256, 444);
            this.preDefiniedNameList.TabIndex = 0;
            this.preDefiniedNameList.DoubleClick += new System.EventHandler(this.preDefName_DoubleClick);
            // 
            // SelectNameForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(271, 458);
            this.Controls.Add(this.preDefiniedNameList);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "SelectNameForm";
            this.Text = "KittyHawk - Select Name";
            this.Load += new System.EventHandler(this.SelectNameForm_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TreeView preDefiniedNameList;
    }
}