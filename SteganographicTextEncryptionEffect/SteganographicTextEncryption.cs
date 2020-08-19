using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Reflection;
using System.Drawing.Text;
using System.Windows.Forms;
using System.IO.Compression;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using Registry = Microsoft.Win32.Registry;
using RegistryKey = Microsoft.Win32.RegistryKey;
using PaintDotNet;
using PaintDotNet.AppModel;
using PaintDotNet.Clipboard;
using PaintDotNet.IndirectUI;
using PaintDotNet.Collections;
using PaintDotNet.PropertySystem;
using PaintDotNet.Effects;
using ColorWheelControl = PaintDotNet.ColorBgra;
using AngleControl = System.Double;
using PanSliderControl = PaintDotNet.Pair<double,double>;
using FilenameControl = System.String;
using ReseedButtonControl = System.Byte;
using RollControl = System.Tuple<double, double, double>;
using IntSliderControl = System.Int32;
using CheckboxControl = System.Boolean;
using TextboxControl = System.String;
using DoubleSliderControl = System.Double;
using ListBoxControl = System.Byte;
using RadioButtonControl = System.Byte;
using MultiLineTextboxControl = System.String;

[assembly: AssemblyTitle("SteganographicTextEncryption plugin for Paint.NET")]
[assembly: AssemblyDescription("Hide Text selected pixels")]
[assembly: AssemblyConfiguration("hide text")]
[assembly: AssemblyCompany("Doug Zwick")]
[assembly: AssemblyProduct("SteganographicTextEncryption")]
[assembly: AssemblyCopyright("Copyright ©2020 by Doug Zwick")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("1.0.*")]

namespace SteganographicTextEncryptionEffect
{
    public class PluginSupportInfo : IPluginSupportInfo
    {
        public string Author
        {
            get
            {
                return base.GetType().Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright;
            }
        }

        public string Copyright
        {
            get
            {
                return base.GetType().Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
            }
        }

        public string DisplayName
        {
            get
            {
                return base.GetType().Assembly.GetCustomAttribute<AssemblyProductAttribute>().Product;
            }
        }

        public Version Version
        {
            get
            {
                return base.GetType().Assembly.GetName().Version;
            }
        }

        public Uri WebsiteUri
        {
            get
            {
                return new Uri("https://www.getpaint.net/redirect/plugins.html");
            }
        }
    }

    [PluginSupportInfo(typeof(PluginSupportInfo), DisplayName = "Hide Text")]
    public class SteganographicTextEncryptionEffectPlugin : PropertyBasedEffect
    {
        public static string StaticName
        {
            get
            {
                return "Hide Text";
            }
        }

        public static Image StaticIcon
        {
            get
            {
                return null;
            }
        }

        public static string SubmenuName
        {
            get
            {
                return "Steganography";
            }
        }

        public SteganographicTextEncryptionEffectPlugin()
            : base(StaticName, StaticIcon, SubmenuName, new EffectOptions() { Flags = EffectFlags.Configurable | EffectFlags.SingleThreaded, RenderingSchedule = EffectRenderingSchedule.None })
        {
        }

        public enum PropertyNames
        {
            M_Text,
            M_Modulus,
            M_Offset
        }


        protected override PropertyCollection OnCreatePropertyCollection()
        {
            List<Property> props = new List<Property>();

            props.Add(new StringProperty(PropertyNames.M_Text, "", 1024));
            props.Add(new Int32Property(PropertyNames.M_Modulus, 7, 1, 1024));
            props.Add(new Int32Property(PropertyNames.M_Offset, 0, 0, 1024));

            return new PropertyCollection(props);
        }

        protected override ControlInfo OnCreateConfigUI(PropertyCollection props)
        {
            ControlInfo configUI = CreateDefaultConfigUI(props);

            configUI.SetPropertyControlValue(PropertyNames.M_Text, ControlInfoPropertyNames.DisplayName, "Input Text");
            configUI.SetPropertyControlValue(PropertyNames.M_Modulus, ControlInfoPropertyNames.DisplayName, "Modulus");
            configUI.SetPropertyControlValue(PropertyNames.M_Offset, ControlInfoPropertyNames.DisplayName, "Offset");

            return configUI;
        }

        protected override void OnCustomizeConfigUIWindowProperties(PropertyCollection props)
        {
            // Change the effect's window title
            props[ControlInfoPropertyNames.WindowTitle].Value = "Steganographic Text Encryption";
            // Add help button to effect UI
            props[ControlInfoPropertyNames.WindowHelpContentType].Value = WindowHelpContentType.PlainText;
            props[ControlInfoPropertyNames.WindowHelpContent].Value = "Hide Text v1.0\nCopyright ©2020 by Doug Zwick\nAll rights reserved.";
            base.OnCustomizeConfigUIWindowProperties(props);
        }

        protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken token, RenderArgs dstArgs, RenderArgs srcArgs)
        {
            m_Text = token.GetProperty<StringProperty>(PropertyNames.M_Text).Value;
            m_Modulus = token.GetProperty<Int32Property>(PropertyNames.M_Modulus).Value;
            m_Offset = token.GetProperty<Int32Property>(PropertyNames.M_Offset).Value;

            base.OnSetRenderInfo(token, dstArgs, srcArgs);
        }

        protected override unsafe void OnRender(Rectangle[] rois, int startIndex, int length)
        {
            if (length == 0) return;
            for (int i = startIndex; i < startIndex + length; ++i)
            {
                Render(DstArgs.Surface,SrcArgs.Surface,rois[i]);
            }
        }

        #region User Entered Code
        // Name: Hide Text
        // Submenu: Steganography
        // Author: Doug Zwick
        // Title: Steganographic Text Encryption
        // Version: 0.1.1
        // Desc:
        // Keywords:
        // URL:
        // Help:
        #region UICode
        TextboxControl m_Text = ""; // [1024]  Input Text
        IntSliderControl m_Modulus = 7; // [1,1024] Modulus
        IntSliderControl m_Offset = 0; // [0,1024] Offset
        #endregion
        
        void Render(Surface dst, Surface src, Rectangle rect)
        {
          if (m_Text.Length <= 0) return;
        
          var charArray = m_Text.ToCharArray();
          var charIndex = 0;
          var pixelIndex = m_Offset;
        
          ColorBgra CurrentPixel;
          for (int y = rect.Top; y < rect.Bottom; y++)
          {
            if (IsCancelRequested) return;
            for (int x = rect.Left; x < rect.Right; x++)
            {
              CurrentPixel = src[x, y];
        
              if (charIndex < charArray.Length && pixelIndex % m_Modulus == 0)
              {
                byte r = CurrentPixel.R;
                byte b = CurrentPixel.B;
        
                byte currentChar = (byte)charArray[charIndex];
        
                if (charIndex % 2 == 0)
                {
                  byte currentChar01 = (byte)(currentChar & 0b00000011);
                  byte currentChar23 = (byte)(currentChar & 0b00001100);
        
                  r = (byte)((r & ~0b00000011) | currentChar01);
                  b = (byte)((b & ~0b00001100) | currentChar23);
                }
                else
                {
                  byte currentChar45 = (byte)(currentChar & 0b00110000);
                  byte currentChar67 = (byte)(currentChar & 0b11000000);
        
                  r = (byte)((r & ~0b00110000) | currentChar45);
                  b = (byte)((b & ~0b11000000) | currentChar67);
                }
        
                CurrentPixel.R = r;
                CurrentPixel.B = b;
        
                ++charIndex;
              }
        
              dst[x, y] = CurrentPixel;
        
              ++pixelIndex;
            }
          }
        }
        
        #endregion
    }
}
