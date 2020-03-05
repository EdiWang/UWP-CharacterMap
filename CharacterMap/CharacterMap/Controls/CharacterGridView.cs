﻿using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Core.Direct;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls
{
    internal class CharacterGridViewTemplateSettings
    {
        public FontFamily FontFamily { get; set; }
        public CanvasFontFace FontFace { get; set; }
        public TypographyFeatureInfo Typography { get; set;}
        public bool ShowColorGlyphs { get; set; }
        public double Size { get; set; }
        public bool EnableReposition { get; set; }
        public bool ShowUnicode { get; set; }
    }


    public class CharacterGridView : GridView
    {
        #region Dependency Properties

        #region ItemSize

        public double ItemSize
        {
            get { return (double)GetValue(ItemSizeProperty); }
            set { SetValue(ItemSizeProperty, value); }
        }

        public static readonly DependencyProperty ItemSizeProperty =
            DependencyProperty.Register(nameof(ItemSize), typeof(double), typeof(CharacterGridView), new PropertyMetadata(0d, (d, e) =>
            {
                ((CharacterGridView)d)._templateSettings.Size = (double)e.NewValue;
            }));

        #endregion

        #region ItemFontFamily

        public FontFamily ItemFontFamily
        {
            get { return (FontFamily)GetValue(ItemFontFamilyProperty); }
            set { SetValue(ItemFontFamilyProperty, value); }
        }

        public static readonly DependencyProperty ItemFontFamilyProperty =
            DependencyProperty.Register(nameof(ItemFontFamily), typeof(FontFamily), typeof(CharacterGridView), new PropertyMetadata(null, (d, e) =>
            {
                ((CharacterGridView)d)._templateSettings.FontFamily = (FontFamily)e.NewValue;
            }));

        #endregion

        #region ItemFontFace

        public CanvasFontFace ItemFontFace
        {
            get { return (CanvasFontFace)GetValue(ItemFontFaceProperty); }
            set { SetValue(ItemFontFaceProperty, value); }
        }

        public static readonly DependencyProperty ItemFontFaceProperty =
            DependencyProperty.Register(nameof(ItemFontFace), typeof(CanvasFontFace), typeof(CharacterGridView), new PropertyMetadata(null, (d, e) =>
            {
                ((CharacterGridView)d)._templateSettings.FontFace = (CanvasFontFace)e.NewValue;
            }));

        #endregion

        #region ItemTypography

        public TypographyFeatureInfo ItemTypography
        {
            get { return (TypographyFeatureInfo)GetValue(ItemTypographyProperty); }
            set { SetValue(ItemTypographyProperty, value); }
        }

        public static readonly DependencyProperty ItemTypographyProperty =
            DependencyProperty.Register(nameof(ItemTypography), typeof(TypographyFeatureInfo), typeof(CharacterGridView), new PropertyMetadata(null, (d, e) =>
            {
                if (d is CharacterGridView g && e.NewValue is TypographyFeatureInfo t)
                {
                    g._templateSettings.Typography = t;
                    g.UpdateTypographies(t);
                }
            }));

        #endregion

        #region ShowColorGlyphs

        public bool ShowColorGlyphs
        {
            get { return (bool)GetValue(ShowColorGlyphsProperty); }
            set { SetValue(ShowColorGlyphsProperty, value); }
        }

        public static readonly DependencyProperty ShowColorGlyphsProperty =
            DependencyProperty.Register(nameof(ShowColorGlyphs), typeof(bool), typeof(CharacterGridView), new PropertyMetadata(false, (d, e) =>
            {
                if (d is CharacterGridView g && e.NewValue is bool b)
                {
                    g._templateSettings.ShowColorGlyphs = b;
                    g.UpdateColorsFonts(b);
                }
            }));

        #endregion

        #region ShowUnicodeDescription

        public bool ShowUnicodeDescription
        {
            get { return (bool)GetValue(ShowUnicodeDescriptionProperty); }
            set { SetValue(ShowUnicodeDescriptionProperty, value); }
        }

        public static readonly DependencyProperty ShowUnicodeDescriptionProperty =
            DependencyProperty.Register(nameof(ShowUnicodeDescription), typeof(bool), typeof(CharacterGridView), new PropertyMetadata(false, (d, e) =>
            {
                if (d is CharacterGridView g && e.NewValue is bool b)
                {
                    g._templateSettings.ShowUnicode = b;
                    g.UpdateUnicode(b);
                }
            }));

        #endregion

        #region EnableResizeAnimation

        public bool EnableResizeAnimation
        {
            get { return (bool)GetValue(EnableResizeAnimationProperty); }
            set { SetValue(EnableResizeAnimationProperty, value); }
        }

        public static readonly DependencyProperty EnableResizeAnimationProperty =
            DependencyProperty.Register(nameof(EnableResizeAnimation), typeof(bool), typeof(CharacterGridView), new PropertyMetadata(false, (d, e) =>
            {
                if (d is CharacterGridView g && e.NewValue is bool b)
                {
                    g._templateSettings.EnableReposition = b;
                    g.UpdateAnimation(b);
                }
            }));

        #endregion

        #endregion

        private XamlDirect _xamlDirect { get; }

        private CharacterGridViewTemplateSettings _templateSettings { get; }

        private ImplicitAnimationCollection _repositionCollection = null;

        public CharacterGridView()
        {
            _xamlDirect = XamlDirect.GetDefault();
            _templateSettings = new CharacterGridViewTemplateSettings();

            this.ContainerContentChanging += OnContainerContentChanging;
            this.ChoosingItemContainer += OnChoosingItemContainer;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            /* 
             * For performance reasons, we've forgone XAML bindings and
             * will update everything in code 
             */
            if (!args.InRecycleQueue && args.ItemContainer is GridViewItem item)
            {
                Character c = ((Character)args.Item);
                UpdateContainer(item, c);
                args.Handled = true;
            }

            if (_templateSettings.EnableReposition)
            {
                if (args.InRecycleQueue)
                {
                    PokeUIElementZIndex(args.ItemContainer);
                }
                else
                {
                    var v = ElementCompositionPreview.GetElementVisual(args.ItemContainer);
                    v.ImplicitAnimations = EnsureRepositionCollection(v.Compositor);
                }
            }
        }




        #region Item Template Handling

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void UpdateContainer(GridViewItem item, Character c)
        {
            // Perf considerations:
            // 1 - Batch rendering updates by suspending rendering until all properties are set
            // 2 - Use XAML direct to set new properties, rather than through DP's
            // 3 - Access any required data properties from parents through normal properties, 
            //     not DP's - DP access can be order of magnitudes slower.

            // Assumed Structure:
            // -- Grid
            //    -- TextBlock
            //    -- TextBlock

            XamlBindingHelper.SuspendRendering(item);

            double size = _templateSettings.Size;
            IXamlDirectObject go = _xamlDirect.GetXamlDirectObject(item.ContentTemplateRoot);

            _xamlDirect.SetObjectProperty(go, XamlPropertyIndex.FrameworkElement_Tag, c);
            _xamlDirect.SetDoubleProperty(go, XamlPropertyIndex.FrameworkElement_Width, size);
            _xamlDirect.SetDoubleProperty(go, XamlPropertyIndex.FrameworkElement_Height, size);

            IXamlDirectObject cld = _xamlDirect.GetXamlDirectObjectProperty(go, XamlPropertyIndex.Panel_Children);
            IXamlDirectObject o = _xamlDirect.GetXamlDirectObjectFromCollectionAt(cld, 0);

            _xamlDirect.SetObjectProperty(o, XamlPropertyIndex.TextBlock_FontFamily, _templateSettings.FontFamily);
            _xamlDirect.SetEnumProperty(o, XamlPropertyIndex.TextBlock_FontStretch, (uint)_templateSettings.FontFace.Stretch);
            _xamlDirect.SetEnumProperty(o, XamlPropertyIndex.TextBlock_FontStyle, (uint)_templateSettings.FontFace.Style);
            _xamlDirect.SetObjectProperty(o, XamlPropertyIndex.TextBlock_FontWeight, _templateSettings.FontFace.Weight);
            _xamlDirect.SetBooleanProperty(o, XamlPropertyIndex.TextBlock_IsColorFontEnabled, _templateSettings.ShowColorGlyphs);
            _xamlDirect.SetDoubleProperty(o, XamlPropertyIndex.TextBlock_FontSize, size / 2d);

            UpdateColorFont(null, o, _templateSettings.ShowColorGlyphs);
            UpdateTypography(o, _templateSettings.Typography);

            _xamlDirect.SetStringProperty(o, XamlPropertyIndex.TextBlock_Text, c.Char);

            IXamlDirectObject o2 = _xamlDirect.GetXamlDirectObjectFromCollectionAt(cld, 1);
            if (_templateSettings.ShowUnicode)
            {
                _xamlDirect.SetStringProperty(o2, XamlPropertyIndex.TextBlock_Text, c.UnicodeString);
                _xamlDirect.SetEnumProperty(o2, XamlPropertyIndex.UIElement_Visibility, 0);
            }
            else
            {
                _xamlDirect.SetEnumProperty(o2, XamlPropertyIndex.UIElement_Visibility, 1);
            }

            XamlBindingHelper.ResumeRendering(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void UpdateColorFont(TextBlock block, IXamlDirectObject xd, bool value)
        {
            if (xd != null)
                _xamlDirect.SetBooleanProperty(xd, XamlPropertyIndex.TextBlock_IsColorFontEnabled, value);
            else
                block.IsColorFontEnabled = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateTypography(IXamlDirectObject o, TypographyFeatureInfo info)
        {
            CanvasTypographyFeatureName f = info == null ? CanvasTypographyFeatureName.None : info.Feature;
            TypographyBehavior.SetTypography(o, f, _xamlDirect);
        }

        void UpdateColorsFonts(bool value)
        {
            if (ItemsSource == null || ItemsPanelRoot == null)
                return;

            foreach (GridViewItem item in ItemsPanelRoot.Children.Cast<GridViewItem>())
            {
                Grid g = (Grid)item.ContentTemplateRoot;
                TextBlock tb = (TextBlock)g.Children[0];
                UpdateColorFont(tb, null, value);
            }
        }

        void UpdateTypographies(TypographyFeatureInfo info)
        {
            if (ItemsSource == null || ItemsPanelRoot == null)
                return;

            foreach (GridViewItem item in ItemsPanelRoot.Children.Cast<GridViewItem>())
            {
                Grid g = (Grid)item.ContentTemplateRoot;
                TextBlock tb = (TextBlock)g.Children[0];
                IXamlDirectObject o = _xamlDirect.GetXamlDirectObject(tb);
                UpdateTypography(o, info);
            }
        }

        void UpdateUnicode(bool value)
        {
            if (ItemsSource == null || ItemsPanelRoot == null)
                return;

            foreach (GridViewItem item in ItemsPanelRoot.Children.Cast<GridViewItem>())
            {
                Grid g = (Grid)item.ContentTemplateRoot;
                if (g.Tag is Character c)
                {
                    TextBlock tb = (TextBlock)g.Children[1];
                    tb.Text = c.UnicodeString;
                    tb.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        public void UpdateSize(double value)
        {
            if (ItemsSource == null || ItemsPanelRoot == null)
                return;

            ItemSize = value;
            foreach (GridViewItem item in ItemsPanelRoot.Children.Cast<GridViewItem>())
            {
                Grid g = (Grid)item.ContentTemplateRoot;
                g.Width = value;
                g.Height = value;
                ((TextBlock)g.Children[0]).FontSize = value / 2d;
            }
        }

        #endregion




        #region Reposition Animation

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (_templateSettings.EnableReposition && args.ItemContainer != null)
            {
                PokeUIElementZIndex(args.ItemContainer);
            }
        }

        private void UpdateAnimation(bool newValue)
        {
            if (this.ItemsPanelRoot == null)
                return;

            foreach (var item in this.ItemsPanelRoot.Children)
            {
                var v = ElementCompositionPreview.GetElementVisual(item);
                v.ImplicitAnimations = newValue ? EnsureRepositionCollection(v.Compositor) : null;
            }
        }

        private void PokeUIElementZIndex(UIElement e)
        {
            var o = _xamlDirect.GetXamlDirectObject(e);
            var i = _xamlDirect.GetInt32Property(o, XamlPropertyIndex.Canvas_ZIndex);
            _xamlDirect.SetInt32Property(o, XamlPropertyIndex.Canvas_ZIndex, i + 1);
            _xamlDirect.SetInt32Property(o, XamlPropertyIndex.Canvas_ZIndex, i);
        }

        private ImplicitAnimationCollection EnsureRepositionCollection(Compositor c)
        {
            if (_repositionCollection == null)
            {
                var offsetAnimation = c.CreateVector3KeyFrameAnimation();
                offsetAnimation.InsertExpressionKeyFrame(1f, "this.FinalValue");
                offsetAnimation.Duration = TimeSpan.FromSeconds(Composition.DefaultOffsetDuration);
                offsetAnimation.Target = nameof(Visual.Offset);

                var g = c.CreateAnimationGroup();
                g.Add(offsetAnimation);

                var s = c.CreateImplicitAnimationCollection();
                s.Add(nameof(Visual.Offset), g);
                _repositionCollection = s;
            }

            return _repositionCollection;
        }

        #endregion
    }
}