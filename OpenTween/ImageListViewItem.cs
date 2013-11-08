﻿// OpenTween - Client of Twitter
// Copyright (c) 2007-2011 kiri_feather (@kiri_feather) <kiri.feather@gmail.com>
//           (c) 2008-2011 Moz (@syo68k)
//           (c) 2008-2011 takeshik (@takeshik) <http://www.takeshik.org/>
//           (c) 2010-2011 anis774 (@anis774) <http://d.hatena.ne.jp/anis774/>
//           (c) 2010-2011 fantasticswallow (@f_swallow) <http://twitter.com/f_swallow>
//           (c) 2011      Egtra (@egtra) <http://dev.activebasic.com/egtra/>
// All rights reserved.
//
// This file is part of OpenTween.
//
// This program is free software; you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the Free
// Software Foundation; either version 3 of the License, or (at your option)
// any later version.
//
// This program is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License
// for more details.
//
// You should have received a copy of the GNU General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>, or write to
// the Free Software Foundation, Inc., 51 Franklin Street - Fifth Floor,
// Boston, MA 02110-1301, USA.

using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Threading;

namespace OpenTween
{
    public class ImageListViewItem : ListViewItem
    {
        protected readonly ImageCache imageCache;
        protected readonly string imageUrl;

        private WeakReference imageReference = new WeakReference(null);

        public event EventHandler ImageDownloaded;

        public ImageListViewItem(string[] items)
            : this(items, null, null)
        {
        }

        public ImageListViewItem(string[] items, ImageCache imageCache, string imageUrl)
            : base(items, imageUrl)
        {
            this.imageCache = imageCache;
            this.imageUrl = imageUrl;

            if (imageCache != null)
            {
                var image = imageCache.TryGetFromCache(imageUrl);

                if (image == null)
                    this.GetImageAsync();
                else
                    this.imageReference.Target = image;
            }
        }

        private Task GetImageAsync(bool force = false)
        {
            if (string.IsNullOrEmpty(this.imageUrl))
                return Task.Factory.StartNew(() => { });

            var uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            return this.imageCache.DownloadImageAsync(this.imageUrl, force)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        t.Exception.Handle(x => x is InvalidImageException);
                        return;
                    }

                    this.imageReference.Target = t.Result;

                    if (this.ListView == null || !this.ListView.Created || this.ListView.IsDisposed)
                        return;

                    if (this.Index < this.ListView.VirtualListSize)
                    {
                        this.ListView.RedrawItems(this.Index, this.Index, true);

                        if (this.ImageDownloaded != null)
                            this.ImageDownloaded(this, EventArgs.Empty);
                    }
                }, uiScheduler);
        }

        public MemoryImage Image
        {
            get
            {
                return (MemoryImage)this.imageReference.Target;
            }
        }

        public void RefreshImage()
        {
            this.imageReference.Target = null;
            this.GetImageAsync(true);
        }
    }
}
