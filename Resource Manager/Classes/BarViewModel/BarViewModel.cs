﻿using Pfim;
using Resource_Manager.Classes.Alz4;
using Resource_Manager.Classes.Bar;
using Resource_Manager.Classes.Ddt;
using Resource_Manager.Classes.L33TZip;
using Resource_Manager.Classes.sound;
using Resource_Manager.Classes.Xmb;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageFormat = Pfim.ImageFormat;

namespace Archive_Unpacker.Classes.BarViewModel
{
    public class BarViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private CollectionViewSource entriesCollection;
        private string filterText;

        public ICollectionView SourceCollection
        {
            get
            {
                return this.entriesCollection.View;
            }
        }


        public string FilterText
        {
            get
            {
                return filterText;
            }
            set
            {
                filterText = value;
                NotifyPropertyChanged();
                entriesCollection.View.Refresh();
            }
        }

        private double currentProgress;
        public double CurrentProgress
        {
            get
            {
                return currentProgress;
            }
            set
            {
                currentProgress = value;
                NotifyPropertyChanged();
            }
        }


        public int extractingState { get; set; }

        private void ResetProgress()
        {
            CurrentProgress = 0;
        }

        public MemoryStream audio { get; set; }

        public string barFilePath { get; set; }
        public BarFile barFile { get; set; }

        private BitmapImage previewImage;
        public BitmapImage PreviewImage
        {
            get { return previewImage; }
            set
            {
                previewImage = value;
                NotifyPropertyChanged();
            }
        }

        private DdtFile previewDdt;
        public DdtFile PreviewDdt
        {
            get { return previewDdt; }
            set
            {
                previewDdt = value;
                NotifyPropertyChanged();
            }
        }

        public class Document
        {
            public string Text { get; set; }
            public string SyntaxHighlighting { get; set; }
        }

        private Document preview;
        public Document Preview
        {
            get { return preview; }
            set
            {
                preview = value;
                NotifyPropertyChanged();
            }
        }

        private bool isCRC32Checked;
        public bool IsCRC32Checked
        {
            get { return isCRC32Checked; }
            set
            {
                isCRC32Checked = value;
                NotifyPropertyChanged();
            }
        }

        public string barFileName
        {
            get
            {
                if (barFile.barFileHeader.Version == 2)
                    return Path.GetFileName(barFilePath) + " - AoE3: Legacy";
                else
                if (barFile.barFileHeader.Version == 4)
                    return Path.GetFileName(barFilePath) + " - AoE3: Definitive Edition - 1st wave of beta testing";
                else
                    return Path.GetFileName(barFilePath) + " - AoE3: Definitive Edition";
            }
        }

        void Filter(object sender, FilterEventArgs e)
        {
            if (string.IsNullOrEmpty(FilterText))
            {
                e.Accepted = true;
                return;
            }

            var entry = e.Item as BarEntry;
            e.Accepted = entry.FileNameWithRoot.ToLower().Contains(FilterText.ToLower());
        }




        public async Task saveFiles(List<BarEntry> files, string savePath, bool Decompress, CancellationToken token, bool convertDDTToPNG, bool convertDDTToTGA, bool convertXMB, bool OneFolder, bool SavePNGasBMP)
        {
            ResetProgress();
            if (files.Count == 0) return;

            using var input = File.OpenRead(barFilePath);

            long filesSize = files.Sum(x => (long)x.FileSize2);

            foreach (var file in files)
            {
                if (token.IsCancellationRequested)
                {
                    while (extractingState == 1)
                        await Task.Delay(1000);
                }
                if (token.IsCancellationRequested && extractingState == 2)
                {
                    ResetProgress();
                    return;
                }
                // Locate the file within the BAR file.
                input.Seek(file.Offset, SeekOrigin.Begin);

                if (OneFolder)
                {
                    Directory.CreateDirectory(Path.Combine(savePath, Path.GetPathRoot(file.FileNameWithRoot)));
                }
                else
                {
                    Directory.CreateDirectory(Path.Combine(savePath, Path.GetDirectoryName(file.FileNameWithRoot)));
                }

                



                var data = new byte[file.FileSize2];
                await input.ReadAsync(data, 0, data.Length);

                 
                if (file.Extension != ".XMB" && (L33TZipUtils.IsL33TZipFile(data) || Alz4Utils.IsAlz4File(data)) && Decompress)
                {
                    if (Alz4Utils.IsAlz4File(data))
                    {
                        data = await Alz4Utils.ExtractAlz4BytesAsync(data);
                    }
                    else
                    {
                        if (L33TZipUtils.IsL33TZipFile(data))
                            data = await L33TZipUtils.ExtractL33TZippedBytesAsync(data);
                    }
                }

                if (file.Extension == ".WAV" || file.Extension == ".MP3")
                {
                    if (file.isCompressed == 2)
                    {
                        data = await soundUtils.DecryptSound(data);
                    }
                }

                if (file.Extension == ".PNG" && SavePNGasBMP)
                {

                    using var memory = new MemoryStream(data);
                    System.Drawing.Image imgFile = System.Drawing.Image.FromStream(memory);
                    
                    if (OneFolder)
                    {
                        imgFile.Save(Path.ChangeExtension(Path.Combine(savePath, Path.GetPathRoot(file.FileNameWithRoot), file.fileNameWithoutPath), "bmp"), System.Drawing.Imaging.ImageFormat.Bmp);
                    }
                    else
                    {
                        imgFile.Save(Path.ChangeExtension(Path.Combine(savePath, file.FileNameWithRoot), "bmp"), System.Drawing.Imaging.ImageFormat.Bmp);
                    }

                }

                if ((file.Extension != ".PNG" && SavePNGasBMP) || (file.Extension == ".PNG" && !SavePNGasBMP))
                {
                    if (OneFolder)
                    {
                        await File.WriteAllBytesAsync(Path.Combine(savePath, Path.GetPathRoot(file.FileNameWithRoot), file.fileNameWithoutPath), data);
                    }
                    else
                    {
                        await File.WriteAllBytesAsync(Path.Combine(savePath, file.FileNameWithRoot), data);
                    }
                }



                if (file.Extension == ".XMB" && convertXMB)
                {
                    if (Alz4Utils.IsAlz4File(data))
                    {
                        data = await Alz4Utils.ExtractAlz4BytesAsync(data);
                    }
                    else
                    {
                        if (L33TZipUtils.IsL33TZipFile(data))
                            data = await L33TZipUtils.ExtractL33TZippedBytesAsync(data);
                    }

                    using MemoryStream stream = new MemoryStream(data);
                    XMBFile xmb = await XMBFile.LoadXMBFile(stream);

                    var newName = Path.ChangeExtension(Path.Combine(savePath, file.FileNameWithRoot), "");
                    if (OneFolder)
                    {
                        newName = Path.ChangeExtension(Path.Combine(savePath, Path.GetPathRoot(file.FileNameWithRoot), file.fileNameWithoutPath), "");
                    }


                    

                    xmb.file.Save(newName);
                }

                if (file.Extension == ".DDT" && convertDDTToPNG)
                {
                    if (OneFolder)
                    {
                        await DdtFileUtils.DdtBytes2PngAsync(data, Path.Combine(savePath, Path.GetPathRoot(file.FileNameWithRoot), file.fileNameWithoutPath));
                    }
                    else
                    {
                        await DdtFileUtils.DdtBytes2PngAsync(data, Path.Combine(savePath, file.FileNameWithRoot));
                    }
                }

                if (file.Extension == ".DDT" && convertDDTToTGA)
                {
                    if (OneFolder)
                    {
                        await DdtFileUtils.DdtBytes2TgaAsync(data, Path.Combine(savePath, Path.GetPathRoot(file.FileNameWithRoot), file.fileNameWithoutPath));
                    }
                    else
                    {
                        await DdtFileUtils.DdtBytes2TgaAsync(data, Path.Combine(savePath, file.FileNameWithRoot));
                    }
                    
                }



                CurrentProgress += (double)file.FileSize2 / filesSize;
            }
            ResetProgress();



        }




        public async Task readFile(BarEntry file)
        {

            // Firstly, is the file parameter null?

            PreviewDdt = null;
            Preview = null;
            PreviewImage = null;
            if (file == null)
                return;

            if (file.Extension == ".WAV" || file.Extension == ".MP3")
            {
                using FileStream input = File.OpenRead(barFilePath);
                // Locate the file within the BAR file.
                input.Seek(file.Offset, SeekOrigin.Begin);
                var data = new byte[file.FileSize2];
                await input.ReadAsync(data, 0, data.Length);

                if (file.isCompressed == 2)
                {
                    audio = new MemoryStream(await soundUtils.DecryptSound(data));
                }
                else
                    audio = new MemoryStream(data);
                return;
            }
            if (file.Extension == ".DDT")
            {
                using FileStream input = File.OpenRead(barFilePath);
                // Locate the file within the BAR file.
                input.Seek(file.Offset, SeekOrigin.Begin);
                var data = new byte[file.FileSize2];
                await input.ReadAsync(data, 0, data.Length);


                if (Alz4Utils.IsAlz4File(data))
                {
                    data = await Alz4Utils.ExtractAlz4BytesAsync(data);
                }
                else
                {
                    if (L33TZipUtils.IsL33TZipFile(data))
                        data = await L33TZipUtils.ExtractL33TZippedBytesAsync(data);
                }



                PreviewDdt = new DdtFile(data, true);
                return;
            }
            if (file.Extension == ".BMP" || file.Extension == ".TGA" || file.Extension == ".PNG" || file.Extension == ".CUR" || file.Extension == ".JPG")
            {
                using FileStream input = File.OpenRead(barFilePath);
                // Locate the file within the BAR file.
                input.Seek(file.Offset, SeekOrigin.Begin);
                var data = new byte[file.FileSize2];
                await input.ReadAsync(data, 0, data.Length);
                if (Alz4Utils.IsAlz4File(data))
                {
                    data = await Alz4Utils.ExtractAlz4BytesAsync(data);
                }
                else
                {
                    if (L33TZipUtils.IsL33TZipFile(data))
                        data = await L33TZipUtils.ExtractL33TZippedBytesAsync(data);
                }
                var bitmap = new BitmapImage();

                using (var stream = new MemoryStream(data))
                {
                    if (file.Extension == ".TGA")
                    {
                        IImage image = await Task.Run(() => Pfim.Pfim.FromStream(stream));
                        var pinnedArray = GCHandle.Alloc(image.Data, GCHandleType.Pinned);
                        var addr = pinnedArray.AddrOfPinnedObject();
                        var bsource = BitmapSource.Create(image.Width, image.Height, 96.0, 96.0,
                            PixelFormat(image), null, addr, image.DataLen, image.Stride);
                        PngBitmapEncoder encoder = new PngBitmapEncoder();
                        MemoryStream memoryStream = new MemoryStream();

                        encoder.Frames.Add(BitmapFrame.Create(bsource));
                        encoder.Save(memoryStream);
                        memoryStream.Position = 0;
                        bitmap.BeginInit();
                        bitmap.StreamSource = memoryStream;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        memoryStream.Close();
                    }
                    /*else if (file.Extension == ".PNG")
                    {
                        System.Drawing.Image image = System.Drawing.Image.FromStream(stream);
                        using var memory = new MemoryStream();
                        image.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                        memory.Seek(0, SeekOrigin.Begin);
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = memory;
                        bitmap.EndInit();
                    }*/
                    else
                    {
                        bitmap.BeginInit();
                        bitmap.StreamSource = stream;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                    }

                }


                PreviewImage = bitmap;
                return;
            }

            if (file.Extension == ".XMB")
            {
                using FileStream input = File.OpenRead(barFilePath);
                // Locate the file within the BAR file.
                input.Seek(file.Offset, SeekOrigin.Begin);
                var data = new byte[file.FileSize2];
                await input.ReadAsync(data, 0, data.Length);

                if (Alz4Utils.IsAlz4File(data))
                {
                    data = await Alz4Utils.ExtractAlz4BytesAsync(data);
                }
                else
                {
                    if (L33TZipUtils.IsL33TZipFile(data))
                        data = await L33TZipUtils.ExtractL33TZippedBytesAsync(data);
                }

                //File.WriteAllBytes(file.fileNameWithoutPath, data);

                Preview = new Document();
                Preview.SyntaxHighlighting = "XML";
                Preview.Text = await XMBFile.XmbToXmlAsync(data);

                NotifyPropertyChanged("Preview");
                return;
            }
            if (file.Extension == ".XAML" || file.Extension == ".XML" || file.Extension == ".SHP" || file.Extension == ".LGT" || file.Extension == ".XS" || file.Extension == ".TXT" || file.Extension == ".CFG" || file.Extension == ".PY" || file.Extension == ".TACTICS")
            {
                using FileStream input = File.OpenRead(barFilePath);
                // Locate the file within the BAR file.
                input.Seek(file.Offset, SeekOrigin.Begin);
                var data = new byte[file.FileSize2];
                await input.ReadAsync(data, 0, data.Length);
                if (Alz4Utils.IsAlz4File(data))
                {
                    data = await Alz4Utils.ExtractAlz4BytesAsync(data);
                }
                else
                {
                    if (L33TZipUtils.IsL33TZipFile(data))
                        data = await L33TZipUtils.ExtractL33TZippedBytesAsync(data);
                }
                Preview = new Document();
                Preview.Text = System.Text.Encoding.UTF8.GetString(data);
                if (file.Extension == ".XS")
                    Preview.SyntaxHighlighting = "C++";
                else
                    Preview.SyntaxHighlighting = "XML";
                NotifyPropertyChanged("Preview");
                return;
            }

            return;
        }

        private static System.Windows.Media.PixelFormat PixelFormat(IImage image)
        {
            switch (image.Format)
            {
                case ImageFormat.Rgb24:
                    return PixelFormats.Bgr24;
                case ImageFormat.Rgba32:
                    return PixelFormats.Bgr32;
                case ImageFormat.Rgb8:
                    return PixelFormats.Gray8;
                case ImageFormat.R5g5b5a1:
                case ImageFormat.R5g5b5:
                    return PixelFormats.Bgr555;
                case ImageFormat.R5g6b5:
                    return PixelFormats.Bgr565;
                default:
                    throw new Exception($"Unable to convert {image.Format} to WPF PixelFormat");
            }
        }
        public async static Task<BarViewModel> Load(string filename, bool doCRC32)
        {
            BarViewModel barViewModel = new BarViewModel();
            barViewModel.extractingState = 0;
            barViewModel.barFilePath = filename;
            barViewModel.IsCRC32Checked = doCRC32;
            barViewModel.barFile = await BarFile.Load(filename, doCRC32);

            barViewModel.ResetProgress();

            barViewModel.entriesCollection = new CollectionViewSource();
            barViewModel.entriesCollection.Source = barViewModel.barFile.BarFileEntrys;
            barViewModel.entriesCollection.Filter += barViewModel.Filter;
            return barViewModel;
        }

        public static async Task<BarViewModel> Create(string rootFolder, uint version)
        {
            BarViewModel barViewModel = new BarViewModel();
            barViewModel.extractingState = 0;

            var filename = rootFolder;
            if (rootFolder.EndsWith(Path.DirectorySeparatorChar.ToString()))
                filename = rootFolder.Substring(0, rootFolder.Length - 1);
            barViewModel.IsCRC32Checked = true;
            barViewModel.barFilePath = filename + ".bar";
            barViewModel.barFile = await BarFile.Create(rootFolder, version);
            barViewModel.entriesCollection = new CollectionViewSource();
            barViewModel.ResetProgress();

            barViewModel.entriesCollection.Source = barViewModel.barFile.BarFileEntrys;
            barViewModel.entriesCollection.Filter += barViewModel.Filter;

            return barViewModel;
        }




    }
}
