﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

using Chronozoom.Entities;

namespace Chronozoom.UI.Utils
{
    /// <summary>
    /// Helper class to generate content item thumbnails.
    /// </summary>
    public class ThumbnailGenerator
    {
        private string _thumbnailsStorage;

        // The max size allowed to be processed for thumbnail source.
        private const int _maxSourceContentLength = 10000000;

        /// <summary>
        /// Constructs a ThumbnailGenerator class
        /// </summary>
        /// <param name="thumbnailsStorage"></param>
        public ThumbnailGenerator(string thumbnailsStorage)
        {
            _thumbnailsStorage = thumbnailsStorage;
        }

        /// <summary>
        /// Creates and uploades to storage thumbnails for the given content item.
        /// </summary>
        /// <param name="contentItem"></param>
        internal void CreateThumbnails(ContentItem contentItem)
        {
            if (contentItem == null || _thumbnailsStorage == null || contentItem.Uri == null || (string.Compare(contentItem.MediaType, "Image", StringComparison.OrdinalIgnoreCase) != 0 && string.Compare(contentItem.MediaType, "skydrive-image", StringComparison.OrdinalIgnoreCase) != 0))
                return;

            // Retrieve storage account information
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(_thumbnailsStorage);

            // Create the blob client.
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve a reference to images container. 
            CloudBlobContainer imagesContainer = blobClient.GetContainerReference("images");
            if (!imagesContainer.Exists())
            {
                imagesContainer.CreateIfNotExists();
                imagesContainer.SetPermissions(
                    new BlobContainerPermissions
                    {
                        PublicAccess = BlobContainerPublicAccessType.Blob
                    });
            }

            // Generate thumbnails
            WebRequest request = WebRequest.Create(contentItem.Uri);
            using (WebResponse response = request.GetResponse())
            {
                Stream responseStream = response.GetResponseStream();
                using (Bitmap bitmap = new Bitmap(responseStream))
                {
                    if (response.ContentLength > _maxSourceContentLength)
                    {
                        throw new InvalidDataException("Source image is too big.");
                    }

                    SaveUploadThumbnail(imagesContainer, contentItem.Id.ToString(), bitmap, 8);
                    SaveUploadThumbnail(imagesContainer, contentItem.Id.ToString(), bitmap, 16);
                    SaveUploadThumbnail(imagesContainer, contentItem.Id.ToString(), bitmap, 32);
                    SaveUploadThumbnail(imagesContainer, contentItem.Id.ToString(), bitmap, 64);
                    SaveUploadThumbnail(imagesContainer, contentItem.Id.ToString(), bitmap, 128);
                }
            }
        }

        private static void SaveUploadThumbnail(CloudBlobContainer imagesContainer, string filename, Bitmap bitmap, int dimension)
        {
            if (imagesContainer == null)
                return;

            using (Bitmap thumbnail = new Bitmap(dimension, dimension))
            {

                Graphics graphics = Graphics.FromImage(thumbnail);
                graphics.InterpolationMode = InterpolationMode.High;
                graphics.DrawImage(bitmap, 0, 0, dimension, dimension);

                using (MemoryStream thumbnailStream = new MemoryStream())
                {
                    thumbnail.Save(thumbnailStream, System.Drawing.Imaging.ImageFormat.Png);

                    // Upload thumbnail
                    CloudBlockBlob blockBlob = imagesContainer.GetBlockBlobReference(@"x" + dimension + @"\" + filename + ".png");

                    thumbnailStream.Seek(0, SeekOrigin.Begin);
                    blockBlob.UploadFromStream(thumbnailStream);
                }
            }
        }
    }
}