using System;
using System.Net.Http;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace XFUploadFile
{
    public partial class MainPage : ContentPage
    {
        private readonly FileUploadClient _fileUploadClient;

        /// <summary>
        /// Determines if the upload should be canceled after sending the next chunk.
        /// </summary>
        private bool _keepUploading = true;

        public MainPage()
        {
            InitializeComponent();
            _fileUploadClient = new FileUploadClient();
        }

        //Uploads the file entirely in one request.
        //NOTE that large files will either cause time outs or you will get a 413 error that indicates the request is too big.
        async void UploadButtonClicked(object sender, EventArgs e)
        {
            var file = await MediaPicker.PickVideoAsync();

            if (file == null)
                return;

            SetControls(true, false);

            var content = new MultipartFormDataContent { { new StreamContent(await file.OpenReadAsync()), "file", file.FileName } };
            var uploadComplete = await _fileUploadClient.UploadFile(content);

            StatusLabel.Text = uploadComplete ? "Upload successful" : "Upload failed";
            SetControls(false, false);
        }

        //Uploads the file using the chunked upload method.
        //This method should have no limit on the size of the file, although it has an overhead in that it needs to send many requests to get the file uploaded.
        async void UploadChunkedButtonClicked(object sender, EventArgs e)
        {
            var file = await MediaPicker.PickVideoAsync();

            if (file == null)
                return;

            SetControls(true, true);

            var openReadAsync = await file.OpenReadAsync();

            var fileHandle = await _fileUploadClient.BeginFileUpload(file.FileName); //Get a file handle from the server to upload chunks to.

            const int maxChunkSize = (512 * 1024); //Send attachments in chunks of 512 KB at a time.
            var buffer = new byte[maxChunkSize];
            long fileSize = 0;
            long totalBytesRead = 0;

            using (var fs = openReadAsync)
            {
                var bytesRead = 0;
                fileSize = fs.Length;
                do
                {
                    var position = fs.Position;
                    bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead < buffer.Length)
                    {
                        //If the bytes read is smaller then the buffer the last chunk will be sent.
                        //Shrink the buffer to fit the last bytes to optimize memory usage.
                        Array.Resize(ref buffer, bytesRead);
                    }

                    await _fileUploadClient.UploadChunk(fileHandle, buffer, position);
                    totalBytesRead += bytesRead;
                    CalculateProgress(totalBytesRead, fileSize); //Update the progress bar.
                } while (bytesRead > 0 && _keepUploading);
            }

            if (!_keepUploading)
            {
                //Cancel the upload
                await _fileUploadClient.EndFileUpload(fileHandle, fileSize, true);
                StatusLabel.Text = "Upload cancelled!";
            }
            else
            {
                var uploadComplete = await _fileUploadClient.EndFileUpload(fileHandle, fileSize);
                StatusLabel.Text = uploadComplete ? "Upload successful" : "Upload failed";
            }

            openReadAsync.Dispose();
            SetControls(false, true);
        }

        //Cancels an active file upload.
        private void CancelButtonClicked(object sender, EventArgs e) => _keepUploading = false;

        /// <summary>
        /// Sets the status of different controls for certain scenario's
        /// </summary>
        /// <param name="startUpload">Determines if the controls should be in the start upload scenario or the end upload scenario.</param>
        /// <param name="isChunked">Determines if the upload is done using the chunks method, if so some more controls are affected.</param>
        private void SetControls(bool startUpload, bool isChunked)
        {
            if (startUpload)
            {
                StatusLabel.Text = "Uploading...";
                UploadButton.IsEnabled = false;
                UploadChunkedButton.IsEnabled = false;
                if (isChunked)
                {
                    UploadProgress.IsVisible = true;
                    CancelButton.IsEnabled = true;
                }
            }
            else
            {
                UploadButton.IsEnabled = true;
                UploadChunkedButton.IsEnabled = true;
                if (isChunked)
                {
                    CancelButton.IsEnabled = false;
                    UploadProgress.IsVisible = false;
                    UploadProgress.Progress = 0;
                    _keepUploading = true;
                }
            }
        }

        /// <summary>
        /// Calculates the progress to show on the upload progress bar using the total file size and the amount already uploaded.
        /// </summary>
        /// <param name="completed">The amount of bytes that have already been uploaded to the server.</param>
        /// <param name="total">The total file size in bytes of the file being uploaded to the server.</param>
        private void CalculateProgress(long completed, long total)
        {
            var comp = Convert.ToDouble(completed);
            var tot = Convert.ToDouble(total);
            var percentage = comp / tot;
            UploadProgress.ProgressTo(percentage, 100, Easing.Linear);
        }
    }
}