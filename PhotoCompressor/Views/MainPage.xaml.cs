using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace PhotoCompressor.Views
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {

        uint Width;
        public ulong Original_byte, Compress_byte;
        public float Totle=0;
        private StorageFile _inputFile;

        public MainPage()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 点击“获取图片”
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Get_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

            //选取一个文件
            var file = await picker.PickSingleFileAsync();
            if (file != null)   //文件不为空则进行下一步
            {
                _inputFile = file;
                await LoadFileAsync(file);
                //弹出压缩选择框
                await IsTure.ShowAsync();
            }
        }

        /// <summary>
        /// 从文件载入图片并显示
        /// </summary>
        /// <param name="file">图片</param>
        private async Task LoadFileAsync(StorageFile file)
        {
            try
            {
                // 显示图片
                BitmapImage src = new BitmapImage();
                using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
                {
                    //从解码器获取像素数据。 我们对解码的像素应用用户请求的变换以利用解码器中的潜在优化。
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                    Original_Size_info.Text ="原始尺寸:"+decoder.PixelHeight.ToString()+"*"+ decoder.OrientedPixelWidth.ToString();
                    Original_Size.Text = "原始尺寸:" + decoder.PixelHeight.ToString() + "*" + decoder.OrientedPixelWidth.ToString();
                    Original_Save.Text = "原始大小:" + stream.Size.ToString();

                    string[] units = new String[] { "B", "KB", "MB", "GB", "TB" };
                    int digitGroups = (int)(Math.Log10(stream.Size) / Math.Log10(1024));
                    Original_Save.Text = "原始大小:" + String.Format("{0:F}", (stream.Size / Math.Pow(1024, digitGroups))) + " " + units[digitGroups];
                    //本次压缩字节数
                    Original_byte = stream.Size;


                    await src.SetSourceAsync(stream);
                }
                Original_Image.Source = src;             

                LongSide.IsEnabled = true;
            //    SaveButton.IsEnabled = true;
            }
            catch (Exception err)
            {
                Debug.WriteLine(err.Message);
            }
        }

        /// <summary>
        /// 选择文件保存位置并进行下一步处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// 

        private async void Save_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {

            FileSavePicker picker = new FileSavePicker();
            picker.FileTypeChoices.Add("JPEG image", new string[] { ".jpg" });
            picker.FileTypeChoices.Add("PNG image", new string[] { ".png" });
            picker.FileTypeChoices.Add("BMP image", new string[] { ".bmp" });
            picker.DefaultFileExtension = ".png";
            picker.SuggestedFileName = "Output Image";
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

            var file = await picker.PickSaveFileAsync();
            if (file != null && !String.IsNullOrEmpty(LongSide.Text))
            {
                uint longSide = uint.Parse(LongSide.Text);
                if (await LoadSaveFileAsync(_inputFile, file, longSide))
                    MsgBox.Text = "压缩成功！" + file.Path;
                else
                    MsgBox.Text = "压缩失败！";
            }
            else if (LongSide.Text =="")
            {
                uint longSide = Width;
                if (await LoadSaveFileAsync(_inputFile, file, longSide))
                    MsgBox.Text = "压缩成功！文件保存在" + file.Path;
                else
                    MsgBox.Text = "压缩失败！";
            }
        }

        /// <summary>
        /// 处理并保存图片
        /// </summary>
        /// <param name="inputFile">输入文件</param>
        /// <param name="outputFile">输出文件</param>
        /// <param name="longSide">长边长度</param>
        /// <returns>成功返回true，否则false。</returns>
        private async Task<bool> LoadSaveFileAsync(StorageFile inputFile, StorageFile outputFile, uint longSide)
        {
            try
            {
                Guid encoderId;
                switch (outputFile.FileType)
                {
                    case ".png":
                        encoderId = BitmapEncoder.PngEncoderId;
                        break;
                    case ".bmp":
                        encoderId = BitmapEncoder.BmpEncoderId;
                        break;
                   
                    case ".jpg":
                    case ".jpeg":
                    default:
                        encoderId = BitmapEncoder.JpegEncoderId;
                        break;
                }

                //图片处理部分
                using (IRandomAccessStream inputStream = await inputFile.OpenAsync(FileAccessMode.Read),
                           outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    //BitmapEncoder需要一个空的输出流; 但是用户可能已经选择了预先存在的文件，所以清零。
                    outputStream.Size = 0;

                    //从解码器获取像素数据。 我们对解码的像素应用用户请求的变换以利用解码器中的潜在优化。
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(inputStream);
                    BitmapTransform transform = new BitmapTransform();

                    //原图尺寸比转换尺寸更小
                    if (decoder.PixelHeight < longSide && decoder.PixelWidth < longSide)
                        throw new Exception("设置的尺寸大于原图尺寸！");
                    // 判断长边并按原图比例确定另一条边的长度
                    if (decoder.PixelHeight > decoder.PixelWidth)
                    {
                        transform.ScaledHeight = longSide;
                        transform.ScaledWidth = (uint)(decoder.PixelWidth * ((float)longSide / decoder.PixelHeight));
                    }
                    else
                    {
                        transform.ScaledHeight = (uint)(decoder.PixelHeight * ((float)longSide / decoder.PixelWidth));
                        transform.ScaledWidth = longSide;
                    }

                    // Fant是相对高质量的插值模式。
                    transform.InterpolationMode = BitmapInterpolationMode.Fant;

                    // BitmapDecoder指示最佳匹配本地存储的图像数据的像素格式和alpha模式。 这可以提供高性能的与或质量增益。
                    BitmapPixelFormat format = decoder.BitmapPixelFormat;
                    BitmapAlphaMode alpha = decoder.BitmapAlphaMode;

                    // PixelDataProvider提供对位图帧中像素数据的访问
                    PixelDataProvider pixelProvider = await decoder.GetPixelDataAsync(
                        format,
                        alpha,
                        transform,
                        ExifOrientationMode.RespectExifOrientation,
                        ColorManagementMode.ColorManageToSRgb
                        );

                    byte[] pixels = pixelProvider.DetachPixelData();

                    //将像素数据写入编码器。
                    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, outputStream);
                    //设置像素数据
                    encoder.SetPixelData(
                        format,
                        alpha,
                        transform.ScaledWidth,
                        transform.ScaledHeight,
                        decoder.DpiX,
                        decoder.DpiY,
                        pixels
                        );

                    await encoder.FlushAsync(); //异步提交和刷新所有图像数据（这一步保存图片到文件）
                    Debug.WriteLine("保存成功：" + outputFile.Path);

                    // 显示图片
                    BitmapImage src = new BitmapImage();
                //    IRandomAccessStream stream = await outputFile.OpenAsync(FileAccessMode.Read);                    
                        //从解码器获取像素数据。 我们对解码的像素应用用户请求的变换以利用解码器中的潜在优化。
                        BitmapDecoder decoder0 = await BitmapDecoder.CreateAsync(outputStream);

                        Compress_Size_info.Text = "压缩尺寸:" + decoder0.PixelHeight.ToString() + "*" + decoder0.OrientedPixelWidth.ToString();

                        string[] units = new String[] { "B", "KB", "MB", "GB", "TB" };
                        int digitGroups = (int)(Math.Log10(outputStream.Size) / Math.Log10(1024));
                        Compress_Save.Text = "压缩大小:" + String.Format("{0:F}",(outputStream.Size / Math.Pow(1024, digitGroups))) + " " + units[digitGroups];
                        //总压缩字节数
                        Compress_byte = outputStream.Size;

                             await src.SetSourceAsync(outputStream);
                                
                    Compress_Image.Source = src;

                    //压缩总结
                    Now_save();
                    Total_save();

                    return true;
                }
            }
            catch (Exception err)
            {
                Debug.WriteLine(err.Message);
                return false;
            }
        }

       void Now_save()
        {
         
            float temp =100-((float)Compress_byte * 100 / Original_byte);

            int digitGroups = (int)(Math.Log10(temp) / Math.Log10(1024));
            Now_Save.Text = "本次压缩掉:" + String.Format("{0:F}",(temp / Math.Pow(1024, digitGroups)))+"%";      
        }
     
        void Total_save()
        {
            ApplicationDataContainer Data = ApplicationData.Current.LocalSettings;

            float temp = (Original_byte - Compress_byte);
            if (Data.Values.ContainsKey("Total_Save"))
            {
                Totle = (float)Data.Values["Total_Save"];
            }
            //压缩累计
            Totle += temp;

            //获取本地应用设置容器
            Data.Values["Total_Save"] = Totle;

            //    int n = (int)Data.Values["Total_Save"];

            string[] units = new String[] { "B", "KB", "MB", "GB", "TB" };
            if (Totle != 0)
            {
                int digitGroups = (int)(Math.Log10(Totle) / Math.Log10(1024));
                Total_Save.Text = "累计压缩掉:" + String.Format("{0:F}", (Totle / Math.Pow(1024, digitGroups))) + " " + units[digitGroups];
            }
            else
            {
                Total_Save.Text = "累计压缩掉:0 MB";
            }
        }

        private void Menu_Click(object sender, RoutedEventArgs e)
        {
            //把Split的打开状态调整为相反
            MenuView.IsPaneOpen = !MenuView.IsPaneOpen;
        }

        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(Setting));
        }

        private void Size_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Size = (ComboBox)sender;
            switch ((Size.SelectedItem as ComboBoxItem).Content.ToString())
            {
                case "480":
                    {
                        Width = 480;
                    };
                    break;
                case "640":
                    {
                        Width = 640;
                    };
                    break;
                case "800":
                    {
                        Width = 800;
                    };
                    break;
                case "1080":
                    {
                        Width = 1080;
                    };
                    break;
                case "1920":
                    {
                        Width = 1920;
                    };
                    break;
                case "2560":
                    {
                        Width = 2560;
                    };
                    break;
                case "3840":
                    {
                        Width = 3840;
                    };
                    break;
                default:; break;
            }
        }
       
        ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            //显示累计压缩量
            Total_save();

            //判断settings容器里面有没有"First"这个键
            if (!settings.Values.ContainsKey("First"))
            { //应用首次启动，必定不会含"First"这个键，让应用导航到GuidePage这个页面，GuidePage这个页面就是对应用的介绍啦
                Frame.Navigate(typeof(GuidePage));
                //在settings容器里面写入"First"这个键值对，应用再次启动时，就不会在导航到介绍页面了。
                settings.Values["First"] = "yes";
            }
            else
            {

            }
        }
        int i = 0;
        private void SetValue_Click(object sender, RoutedEventArgs e)
        {
            if (i == 0)
            {
                Value_Grid.Visibility = Visibility.Visible;
                i += 1;
            }
            else {
                Value_Grid.Visibility = Visibility.Collapsed;
                i -= 1;
            }
        }
    }
}
