﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.IO;

namespace OpenTween
{
    public class OTPictureBox : PictureBox
    {
        [Localizable(true)]
        public new string ImageLocation
        {
            get { return this._ImageLocation; }
            set
            {
                if (value == null)
                {
                    this.Image = null;
                    return;
                }
                this.LoadAsync(value);
            }
        }
        private string _ImageLocation;

        private Task loadAsyncTask = null;
        private CancellationTokenSource loadAsyncCancelTokenSource = null;

        public new Task LoadAsync(string url)
        {
            this._ImageLocation = url;

            if (this.loadAsyncTask != null && !this.loadAsyncTask.IsCompleted)
                this.CancelAsync();

            if (this.expandedInitialImage != null)
                this.Image = this.expandedInitialImage;

            Uri uri;
            try
            {
                uri = new Uri(url);
            }
            catch (UriFormatException)
            {
                uri = new Uri(Path.GetFullPath(url));
            }

            var client = new OTWebClient();

            client.DownloadProgressChanged += (s, e) =>
            {
                this.OnLoadProgressChanged(new ProgressChangedEventArgs(e.ProgressPercentage, e.UserState));
            };

            this.loadAsyncCancelTokenSource = new CancellationTokenSource();
            var cancelToken = this.loadAsyncCancelTokenSource.Token;
            var loadImageTask = client.DownloadDataAsync(uri, cancelToken);

            // UnobservedTaskException イベントを発生させないようにする
            loadImageTask.ContinueWith(t => { var ignore = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);

            var uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            return loadImageTask.ContinueWith(t => {
                if (t.IsFaulted) throw t.Exception;

                var bytes = t.Result;
                using (var stream = new MemoryStream(bytes))
                {
                    stream.Write(bytes, 0, bytes.Length);

                    return Image.FromStream(stream, true, true);
                }
            }, cancelToken)
            .ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    if (t.IsFaulted)
                        this.Image = this.expandedErrorImage;
                    else
                        this.Image = t.Result;
                }

                var exp = t.Exception != null ? t.Exception.Flatten() : null;
                this.OnLoadCompleted(new AsyncCompletedEventArgs(exp, t.IsCanceled, null));
            }, uiScheduler);
        }

        public new void CancelAsync()
        {
            if (this.loadAsyncTask == null || this.loadAsyncTask.IsCompleted)
                return;

            this.loadAsyncCancelTokenSource.Cancel();

            try
            {
                this.loadAsyncTask.Wait();
            }
            catch (AggregateException ae)
            {
                ae.Handle(e =>
                {
                    if (e is OperationCanceledException)
                        return true;
                    if (e is WebException)
                        return true;

                    return false;
                });
            }
        }

        public new Image ErrorImage
        {
            get { return base.ErrorImage; }
            set
            {
                base.ErrorImage = value;
                this.UpdateStatusImages();
            }
        }

        public new Image InitialImage
        {
            get { return base.InitialImage; }
            set
            {
                base.InitialImage = value;
                this.UpdateStatusImages();
            }
        }

        private Image expandedErrorImage = null;
        private Image expandedInitialImage = null;

        /// <summary>
        /// ErrorImage と InitialImage の表示用の画像を生成する
        /// </summary>
        /// <remarks>
        /// ErrorImage と InitialImage は SizeMode の値に依らず中央等倍に表示する必要があるため、
        /// 事前にコントロールのサイズに合わせた画像を生成しておく
        /// </remarks>
        private void UpdateStatusImages()
        {
            if (this.expandedErrorImage != null)
                this.expandedErrorImage.Dispose();

            if (this.expandedInitialImage != null)
                this.expandedInitialImage.Dispose();

            this.expandedErrorImage = this.ExpandImage(this.ErrorImage);
            this.expandedInitialImage = this.ExpandImage(this.InitialImage);
        }

        private Image ExpandImage(Image image)
        {
            if (image == null) return null;

            var bitmap = new Bitmap(this.ClientSize.Width, this.ClientSize.Height);

            using (var g = this.CreateGraphics())
            {
                bitmap.SetResolution(g.DpiX, g.DpiY);
            }

            using (var g = Graphics.FromImage(bitmap))
            {
                var posx = (bitmap.Width - image.Width) / 2;
                var posy = (bitmap.Height - image.Height) / 2;

                g.DrawImage(image,
                    new Rectangle(posx, posy, image.Width, image.Height),
                    new Rectangle(0, 0, image.Width, image.Height),
                    GraphicsUnit.Pixel);
            }

            return bitmap;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.UpdateStatusImages();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (this.expandedErrorImage != null)
                this.expandedErrorImage.Dispose();

            if (this.expandedInitialImage != null)
                this.expandedInitialImage.Dispose();
        }
    }
}
