﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Inscribe.Common;
using Livet;

namespace Inscribe.ViewModels.Common
{
    /// <summary>
    /// 画像を重ねて配置するためのアレ
    /// </summary>
    public class ImageStackingViewViewModel : ViewModel
    {
        public IEnumerable<ImageStackItem> Images
        {
            get { return Layout(this._images ?? new[] { "/Resources/empty.png".ToPackUri() }); }
        }

        private IEnumerable<ImageStackItem> Layout(IEnumerable<Uri> uris)
        {
            int count = 0;
            int max = uris.Count() - 1;
            foreach (var item in uris.Reverse())
            {
                yield return new ImageStackItem(count, max, item);
                count++;
            }
        }

        private IEnumerable<Uri> _images = null;
        public IEnumerable<Uri> ImageUrls
        {
            get { return this._images; }
            set
            {
                if (this._images == value) return;
                if (value == null || value.Count() == 0)
                    this._images = null;
                else
                    this._images = value.ToArray();
                RaisePropertyChanged(() => ImageUrls);
                RaisePropertyChanged(() => Images);
            }
        }
    }

    public class ImageStackItem : ViewModel
    {
        int layer;
        int layerMax;
        public ImageStackItem(int layer, int layerMax, Uri imageSource)
        {
            this.layer = layer;
            this.layerMax = layerMax;
            this.ImageSource = imageSource;
        }

        public Thickness Margin
        {
            get
            {
                var lt = (layerMax - layer) * 10;
                var rb = layer * 10;
                return new Thickness(lt, lt, rb, rb);
            }
        }

        public Uri ImageSource { get; set; }

    }
}
