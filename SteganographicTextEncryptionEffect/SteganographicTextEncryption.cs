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
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using Registry = Microsoft.Win32.Registry;
using RegistryKey = Microsoft.Win32.RegistryKey;
using PaintDotNet;
using PaintDotNet.Effects;
using PaintDotNet.AppModel;
using PaintDotNet.IndirectUI;
using PaintDotNet.Collections;
using PaintDotNet.PropertySystem;
using IntSliderControl = System.Int32;
using CheckboxControl = System.Boolean;
using ColorWheelControl = PaintDotNet.ColorBgra;
using AngleControl = System.Double;
using PanSliderControl = PaintDotNet.Pair<double, double>;
using TextboxControl = System.String;
using FilenameControl = System.String;
using DoubleSliderControl = System.Double;
using ListBoxControl = System.Byte;
using RadioButtonControl = System.Byte;
using ReseedButtonControl = System.Byte;
using MultiLineTextboxControl = System.String;
using RollControl = System.Tuple<double, double, double>;

[assembly: AssemblyTitle("SteganographicTextEncryption plugin for paint.net")]
[assembly: AssemblyDescription("Hide Text selected pixels")]
[assembly: AssemblyConfiguration("hide text")]
[assembly: AssemblyCompany("Doug Zwick")]
[assembly: AssemblyProduct("SteganographicTextEncryption")]
[assembly: AssemblyCopyright("Copyright ©2019 by Doug Zwick")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("0.1.*")]

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
    //public StringProperty InputText;
    //public Int32Property Modulus;
    //public Int32Property Offset;

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
        : base(StaticName, StaticIcon, SubmenuName, EffectFlags.Configurable)
    {
    }

    public enum PropertyNames
    {
      InputText,
      Modulus,
      Offset,
    }


    protected override PropertyCollection OnCreatePropertyCollection()
    {
      List<Property> props = new List<Property>();

      props.Add(new StringProperty(PropertyNames.InputText, "Hello, World."));
      props.Add(new Int32Property(PropertyNames.Modulus, 7, 1, 100));
      props.Add(new Int32Property(PropertyNames.Offset, 0, 1, 100));

      return new PropertyCollection(props);
    }

    protected override ControlInfo OnCreateConfigUI(PropertyCollection props)
    {
      ControlInfo configUI = CreateDefaultConfigUI(props);

      configUI.SetPropertyControlValue(PropertyNames.InputText, ControlInfoPropertyNames.DisplayName, "Text");
      configUI.SetPropertyControlValue(PropertyNames.Modulus, ControlInfoPropertyNames.DisplayName, "Modulus");
      configUI.SetPropertyControlValue(PropertyNames.Offset, ControlInfoPropertyNames.DisplayName, "Offset");

      return configUI;
    }

    protected override void OnCustomizeConfigUIWindowProperties(PropertyCollection props)
    {
      // Change the effect's window title
      props[ControlInfoPropertyNames.WindowTitle].Value = "Steganographic Text Encryption";
      // Add help button to effect UI
      props[ControlInfoPropertyNames.WindowHelpContentType].Value = WindowHelpContentType.PlainText;
      props[ControlInfoPropertyNames.WindowHelpContent].Value = "Hide Text v0.1\nCopyright ©2019 by Doug Zwick\nAll rights reserved.";
      base.OnCustomizeConfigUIWindowProperties(props);
    }

    protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken newToken, RenderArgs dstArgs, RenderArgs srcArgs)
    {
      InputText = newToken.GetProperty<StringProperty>(PropertyNames.InputText).Value;
      Modulus = newToken.GetProperty<Int32Property>(PropertyNames.Modulus).Value;
      Offset = newToken.GetProperty<Int32Property>(PropertyNames.Offset).Value;

      base.OnSetRenderInfo(newToken, dstArgs, srcArgs);
    }

    protected override unsafe void OnRender(Rectangle[] rois, int startIndex, int length)
    {
      if (length == 0) return;
      for (int i = startIndex; i < startIndex + length; ++i)
      {
        Render(DstArgs.Surface, SrcArgs.Surface, rois[i]);
      }
    }

    #region User Entered Code
    // Name:      Hide Text
    // Submenu:   Steganography
    // Author:    Doug Zwick
    // Title:     Steganographic Text Encryption
    // Version:   0.1
    // Desc:      
    // Keywords:  
    // URL:       
    // Help:      
    #region UICode
    string InputText = "Hello, World!";
    int Modulus = 7;
    int Offset = 0;
    #endregion

    void Render(Surface dst, Surface src, Rectangle rect)
    {
      // Delete any of these lines you don't need
      Rectangle selection = EnvironmentParameters.GetSelection(src.Bounds).GetBoundsInt();
      int CenterX = ((selection.Right - selection.Left) / 2) + selection.Left;
      int CenterY = ((selection.Bottom - selection.Top) / 2) + selection.Top;
      ColorBgra PrimaryColor = EnvironmentParameters.PrimaryColor;
      ColorBgra SecondaryColor = EnvironmentParameters.SecondaryColor;
      int BrushWidth = (int)EnvironmentParameters.BrushWidth;

      if (InputText.Length <= 0) return;

      var charArray = InputText.ToCharArray();
      var charIndex = 0;
      var pixelIndex = Offset;

      ColorBgra CurrentPixel;
      for (int y = rect.Top; y < rect.Bottom; y++)
      {
        if (IsCancelRequested) return;
        for (int x = rect.Left; x < rect.Right; x++)
        {
          CurrentPixel = src[x, y];

          if (charIndex < charArray.Length && pixelIndex % Modulus == 0)
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
