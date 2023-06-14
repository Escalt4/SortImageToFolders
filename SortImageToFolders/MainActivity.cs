using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Net;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Java.Interop;
using Java.Lang;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SortImageToFolders
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        private const int RequestAllFilesAccess = 1;

        // ширина экрана
        private int screenWidth;

        // инициализация настроек
        private ISharedPreferences settings;
        private ISharedPreferencesEditor prefEditor;
        // переменные для хранения настроек
        private bool invertUI;
        private bool reverseFileOrder;

        //
        private TextView textViewSplashScreen;
        private LinearLayout linearLayoutPanel;
        private LinearLayout linearLayoutPanel2;
        private ImageView imageView;
        private TextView textViewProgress;
        private LinearLayout linearLayoutMain;

        private Button buttonApplyChanges;
        private Button buttonUndo;
        private Button buttonDel;
        private Button buttonSkip;
        private Button buttonToA;
        private Button buttonToB;

        // пути к папкам для сортировки
        private string pathA;
        private string pathB;

        // список изображений 
        private List<ImageData> imageList = new List<ImageData>();
        // индекс текущего изображения
        private int curImageIndex = 0;


        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (requestCode == RequestAllFilesAccess)
            {
                // Проверяем, предоставлен ли доступ ко всем файлам
                if (Environment.IsExternalStorageManager)
                {
                    // Доступ ко всем файлам был предоставлен
                    _ = InitApp();
                }
                else
                {
                    // Доступ ко всем файлам не был предоставлен
                    try
                    {
                        Intent intent = new Intent(Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
                        intent.SetData(Android.Net.Uri.FromParts("package", PackageName, null));
                        StartActivityForResult(intent, RequestAllFilesAccess);
                    }
                    catch (Exception)
                    {
                        Intent intent = new Intent(Android.Provider.Settings.ActionManageAllFilesAccessPermission);
                        StartActivityForResult(intent, RequestAllFilesAccess);
                    }
                }
            }
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            // Проверяем, предоставлен ли уже доступ ко всем файлам
            if (!Environment.IsExternalStorageManager)
            {
                try
                {
                    Intent intent = new Intent(Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
                    intent.SetData(Android.Net.Uri.FromParts("package", PackageName, null));
                    StartActivityForResult(intent, RequestAllFilesAccess);
                }
                catch (Exception)
                {
                    Intent intent = new Intent(Android.Provider.Settings.ActionManageAllFilesAccessPermission);
                    StartActivityForResult(intent, RequestAllFilesAccess);
                }

            }
            else
            {
                _ = InitApp();
            }
        }

        // инициализация приложения
        async Task InitApp()
        {
            // получаем ширину экрана для маштабирования слишком больших изображений
            screenWidth = Resources.DisplayMetrics.WidthPixels;

            // инициализируем ссылки на компоненты 
            textViewSplashScreen = FindViewById<TextView>(Resource.Id.textViewSplashScreen);
            linearLayoutPanel = FindViewById<LinearLayout>(Resource.Id.linearLayoutPanel);
            linearLayoutPanel2 = FindViewById<LinearLayout>(Resource.Id.linearLayoutPanel2);
            imageView = FindViewById<ImageView>(Resource.Id.imageView);
            textViewProgress = FindViewById<TextView>(Resource.Id.textViewProgress);
            linearLayoutMain = FindViewById<LinearLayout>(Resource.Id.linearLayoutMain);
            buttonApplyChanges = FindViewById<Button>(Resource.Id.buttonApplyChanges);
            buttonUndo = FindViewById<Button>(Resource.Id.buttonUndo);
            buttonDel = FindViewById<Button>(Resource.Id.buttonDel);
            buttonSkip = FindViewById<Button>(Resource.Id.buttonSkip);
            buttonToA = FindViewById<Button>(Resource.Id.buttonToA);
            buttonToB = FindViewById<Button>(Resource.Id.buttonToB);

            // инициализация настроек
            settings = GetSharedPreferences("Settings", FileCreationMode.Private);
            prefEditor = settings.Edit();

            //
            textViewSplashScreen.Text = "Поиск изображений ...";

            // ищем изображения 
            imageList = await FindImageAsync();

            if (imageList.Count == 0)
            {
                textViewSplashScreen.Text = "Изображений не найдено";
            }
            else
            {
                // скрывает текст и показываем изображение
                textViewSplashScreen.Visibility = ViewStates.Gone;
                linearLayoutPanel.Visibility = ViewStates.Visible;
                imageView.Visibility = ViewStates.Visible;

                // переворачиваем интерфейс если это задано в настройках
                invertUI = settings.GetBoolean("invertUI", false);
                if (invertUI)
                {
                    InvertUI();
                }

                // сортировка по дате изменения 
                imageList.Sort((x, y) => (x.LastModifiedData).CompareTo(y.LastModifiedData));

                // переворачиваем список если задано в настройках
                reverseFileOrder = settings.GetBoolean("reverseFileOrder", true);
                if (reverseFileOrder)
                {
                    imageList.Reverse();
                }

                // инициализация путей к папкам для сортировки
                pathA = InitFolderPath("A");
                pathB = InitFolderPath("B");

                // отображам следующее изображение
                ShowNextImage();


            }
        }

        // Поиск файлов изображений
        async Task<List<ImageData>> FindImageAsync()
        {
            var imageConcurrentBag = new ConcurrentBag<ImageData>();
            await Task.Run(() =>
            {
                string[] filesPaths = Directory.GetFiles(Environment.ExternalStorageDirectory + "/" + "Pictures/Ext/");

                if (filesPaths.Length != 0)
                {
                    if (filesPaths != null)
                    {
                        Parallel.ForEach(filesPaths, file =>
                    {
                        if (IsImage(file))
                        {
                            imageConcurrentBag.Add(new ImageData(new FileInfo(file).Name, file, 0, new FileInfo(file).LastWriteTime));
                        }
                    });
                    }
                }
            });
            return imageConcurrentBag.ToList();
        }

        // Проверка на то что файл - изображение
        bool IsImage(string filePath)
        {
            BitmapFactory.Options options = new BitmapFactory.Options()
            {
                InJustDecodeBounds = true
            };
            BitmapFactory.DecodeFile(filePath, options);

            return options.OutWidth != -1 && options.OutHeight != -1;
        }

        // инициализировать пути к папкам для сортировки
        string InitFolderPath(string folderLetter)
        {
            string externalStorageDirectory = Environment.ExternalStorageDirectory.ToString();
            string pathAsString = string.Empty;

            for (int folderNumber = 0; folderNumber < int.MaxValue; folderNumber++)
            {
                pathAsString = $"{externalStorageDirectory}/Pictures/Ext{folderLetter}{folderNumber}/";

                // если папки несуществует то создаем и используем ее
                if (!Directory.Exists(pathAsString))
                {
                    Directory.CreateDirectory(pathAsString);
                    break;
                }

                // если папка существует но она пустая то используем ее
                if (Directory.GetDirectories(pathAsString).Length == 0 && Directory.GetFiles(pathAsString).Length == 0)
                {
                    break;
                }
            }

            return pathAsString;
        }

        // обработка нажатия на кнопки 
        [Export("MyOnClick")]
        public void MyOnClick(View view)
        {
            switch (view.Id)
            {
                case Resource.Id.buttonToMenu:
                    linearLayoutPanel.Visibility = ViewStates.Gone;
                    linearLayoutPanel2.Visibility = ViewStates.Visible;
                    break;

                case Resource.Id.buttonExitMenu:
                    linearLayoutPanel2.Visibility = ViewStates.Gone;
                    linearLayoutPanel.Visibility = ViewStates.Visible;
                    break;

                case Resource.Id.buttonToA:
                    SetStatus(11);
                    break;

                case Resource.Id.buttonToB:
                    SetStatus(12);
                    break;

                case Resource.Id.buttonDel:
                    SetStatus(2);
                    break;

                case Resource.Id.buttonSkip:
                    SetStatus(3);
                    break;

                case Resource.Id.buttonUndo:
                    SetStatus(0);
                    break;

                case Resource.Id.buttonInvertUI:
                    InvertUI();
                    linearLayoutPanel2.Visibility = ViewStates.Gone;
                    linearLayoutPanel.Visibility = ViewStates.Visible;
                    break;

                case Resource.Id.buttonReverseImageList:
                    DoReverse();
                    linearLayoutPanel2.Visibility = ViewStates.Gone;
                    linearLayoutPanel.Visibility = ViewStates.Visible;
                    break;

                case Resource.Id.buttonApplyChanges:
                    ApplyChanges();
                    linearLayoutPanel2.Visibility = ViewStates.Gone;
                    linearLayoutPanel.Visibility = ViewStates.Visible;
                    break;

            }
        }

        // установить статус изображению
        void SetStatus(int status)
        {
            if (curImageIndex < imageList.Count)
            {
                imageList[curImageIndex].Status = status;
            }

            if (status == 0)
            {
                if (curImageIndex > 0)
                {
                    curImageIndex -= 1;
                }
            }
            else
            {
                if (curImageIndex < imageList.Count)
                {
                    curImageIndex += 1;
                }
            }
            ShowNextImage();
        }

        // показать cледующее изображение
        void ShowNextImage()
        {
            textViewProgress.Text = $"Просмотрено:\n{curImageIndex}/{imageList.Count}";

            if (curImageIndex == 0)
            {
                buttonApplyChanges.Enabled = false;
                buttonUndo.Enabled = false;
                buttonDel.Enabled = true;
                buttonSkip.Enabled = true;
                buttonToA.Enabled = true;
                buttonToB.Enabled = true;
            }
            else if (curImageIndex == imageList.Count)
            {
                buttonApplyChanges.Enabled = true;
                buttonUndo.Enabled = true;
                buttonDel.Enabled = false;
                buttonSkip.Enabled = false;
                buttonToA.Enabled = false;
                buttonToB.Enabled = false;
            }
            else
            {
                buttonApplyChanges.Enabled = true;
                buttonUndo.Enabled = true;
                buttonDel.Enabled = true;
                buttonSkip.Enabled = true;
                buttonToA.Enabled = true;
                buttonToB.Enabled = true;
            }

            if (curImageIndex < imageList.Count)
            {
                textViewSplashScreen.Visibility = ViewStates.Gone;
                imageView.Visibility = ViewStates.Visible;

                Bitmap image = BitmapFactory.DecodeFile(imageList[curImageIndex].ImagePath);

                int width = image.Width;
                int height = image.Height;

                if (width * height * 4 >= 100 * 1024 * 1024)
                {
                    image = GetResizedImage(image, width, height);
                }

                imageView.SetImageBitmap(image);
            }
            else if (curImageIndex == imageList.Count)
            {
                textViewSplashScreen.Text = "Все изображения просмотрены";
                imageView.Visibility = ViewStates.Gone;
                textViewSplashScreen.Visibility = ViewStates.Visible;
            }
        }

        // ресайз изображения
        Bitmap GetResizedImage(Bitmap image, float width, float height)
        {
            float scale = Math.Max(screenWidth / width, screenWidth / height);

            int newWidth = (int)(width * scale);
            int newHeight = (int)(height * scale);

            Bitmap resizedImage = Bitmap.CreateScaledBitmap(image, newWidth, newHeight, true);
            image.Recycle();

            return resizedImage;
        }

        // перевернуть интерфейс
        void InvertUI()
        {
            invertUI = !invertUI;
            prefEditor.PutBoolean("invertUI", invertUI);
            prefEditor.Apply();
            prefEditor.Commit();

            FlipLinearLayout(linearLayoutMain);
            FlipLinearLayout(linearLayoutPanel);
            FlipLinearLayout(linearLayoutPanel2);
        }

        // перевернуть LinearLayout
        void FlipLinearLayout(LinearLayout linearLayout)
        {
            linearLayout.RemoveAllViews();
            int childCount = linearLayout.ChildCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                View child = linearLayout.GetChildAt(i);
                linearLayout.AddView(child);
            }

            //int childCount = linearLayout.ChildCount;
            //View[] children = new View[childCount];
            //for (int i = 0; i < childCount; i++)
            //{
            //    children[i] = linearLayout.GetChildAt(i);
            //}
            //linearLayout.RemoveAllViews();

            //Array.Sort(children, (x, y) => x.Id.CompareTo(y.Id));

            //for (int i = 0; i < childCount; i++)
            //{
            //    linearLayout.AddView(children[childCount - 1 - i]);
            //}
        }

        // перевернуть список изображений
        void DoReverse()
        {
            imageList.Reverse(curImageIndex + 1, imageList.Count - curImageIndex - 1);

            reverseFileOrder = !reverseFileOrder;

            prefEditor.PutBoolean("reverseFileOrder", reverseFileOrder);
            prefEditor.Apply();
            prefEditor.Commit();
        }

        // применить изменения к изображениям
        void ApplyChanges()
        {
            linearLayoutPanel.Visibility = ViewStates.Gone;
            imageView.Visibility = ViewStates.Gone;
            textViewSplashScreen.Visibility = ViewStates.Visible;
            textViewSplashScreen.Text = "Применение выбранных действий";

            ImageData temp = imageList[curImageIndex];

            int i = imageList.Count - 1;
            while (i >= 0)
            {
                switch (imageList[i].Status)
                {
                    case 11:
                        File.Copy(imageList[i].ImagePath, pathA + "/" + imageList[i].ImageName);
                        File.Delete(imageList[i].ImagePath);
                        imageList.RemoveAt(i);
                        break;

                    case 12:
                        File.Copy(imageList[i].ImagePath, pathB + "/" + imageList[i].ImageName);
                        File.Delete(imageList[i].ImagePath);
                        imageList.RemoveAt(i);
                        break;

                    case 2:
                        File.Delete(imageList[i].ImagePath);
                        imageList.RemoveAt(i);
                        break;
                }
                i--;
            }

            if (imageList.Count == 0)
            {
                linearLayoutPanel.Visibility = ViewStates.Gone;
                imageView.Visibility = ViewStates.Gone;
                textViewSplashScreen.Visibility = ViewStates.Visible;
                textViewSplashScreen.Text = "Все открытые изображения были обработаны";
            }
            else
            {
                textViewSplashScreen.Visibility = ViewStates.Gone;
                linearLayoutPanel.Visibility = ViewStates.Visible;
                imageView.Visibility = ViewStates.Visible;

                curImageIndex = imageList.IndexOf(temp);
                if (curImageIndex == -1)
                {
                    curImageIndex = imageList.Count - 1;
                }
                ShowNextImage();
            }

            textViewProgress.Text = $"{curImageIndex + 1}/{imageList.Count}";
        }
    }
}