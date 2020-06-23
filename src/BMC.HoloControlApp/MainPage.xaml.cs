using BMC.HoloControlApp.Helpers;
using MyToolkit.Multimedia;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using Windows.ApplicationModel.Resources.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechRecognition;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace BMC.HoloControlApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        #region tony
        static List<DeviceData> Devices = DeviceData.GetAllDevices();
        public enum MediaBank { Warning, Info, Alert, Alarm }

        public enum Genre { Rock, Slow, Blues, Jazz, Electro };

        // Speech events may come in on a thread other than the UI thread, keep track of the UI thread's
        // dispatcher, so we can update the UI in a thread-safe manner.
        private CoreDispatcher dispatcher;
        static Timer timer;
        // The speech recognizer used throughout this sample.
        private SpeechRecognizer speechRecognizer;

        Dictionary<MediaBank, MediaItem> listMedia;
        MediaPlayer mp;
        /// <summary>
        /// the HResult 0x8004503a typically represents the case where a recognizer for a particular language cannot
        /// be found. This may occur if the language is installed, but the speech pack for that language is not.
        /// See Settings -> Time & Language -> Region & Language -> *Language* -> Options -> Speech Language Options.
        /// </summary>
        private static uint HResultRecognizerNotFound = 0x8004503a;
        // Webcam Related Variables:
        private WebcamHelper webcam;
        // Speech Related Variables:
        private SpeechHelper speech;
        private ResourceContext speechContext;
        private ResourceMap speechResourceMap;
        private Dictionary<string, string[]> VoiceCommands = null;
        EngineContainer ApiContainer = new EngineContainer();
        static public bool IsRecognizerEnable { set; get; } = false;
        static public bool IsWebCamReady { set; get; } = false;
        static HttpClient httpClient;
        static Dictionary<Genre, string[]> SongIDs = new Dictionary<Genre, string[]>();
        // Keep track of whether the continuous recognizer is currently running, so it can be cleaned up appropriately.
        private bool isListening;


        #endregion
        MqttService clientMqtt;
        public MainPage()
        {
            this.InitializeComponent();
            Setup();
        }

        void Setup()
        {
            BtnExit.Click += (a, b) => { if (this.Frame.CanGoBack) this.Frame.GoBack(); else this.Frame.Navigate(typeof(MainPage)); };
            clientMqtt = new MqttService();
            List1.ItemsSource = DeviceData.GetAllDevices();
            BtnFind.Click += (a, b) => {
                Progress1.Visibility = Visibility.Visible;
                List1.ItemsSource = DeviceData.FilterDevice(TxtSearch.Text);
                Progress1.Visibility = Visibility.Collapsed;
            };
            Inittony();
        }
        #region tony
        void Inittony()
        {
            //BlobEngine = new AzureBlobHelper();
            httpClient = new HttpClient();
            //populate media
            mp = new MediaPlayer();
            //Attach the player to the MediaPlayerElement:
            Player1.SetMediaPlayer(mp);
            listMedia = new Dictionary<MediaBank, MediaItem>()
            {
                [MediaBank.Warning] = new MediaItem("https://archive.org/download/Urecord20140607.20.26.53/Urecord_2014-06-07.20.26.53.mp3"),
                [MediaBank.Alarm] = new MediaItem("https://archive.org/download/Queen-WeWillRockYou_688/Queen-WeWillRockYou.mp3"),//(@"ms-appx:///lagu/metal.mp3"),
                [MediaBank.Alert] = new MediaItem("https://archive.org/download/Urecord20140607.20.26.53/Urecord_2014-06-07.20.26.53.mp3"),//(@"ms -appx:///lagu/lucu.mp3"),
                [MediaBank.Info] = new MediaItem("https://archive.org/download/HotelCalifornia_201610/Hotel%20California.mp3")//(@"ms -appx:///lagu/santai.mp3")
            };

            SongIDs.Add(Genre.Slow, new string[] { "C38MvuAtZjc", "qCGrJ7zVA7U", "DCMd-P9cxBo" });
            SongIDs.Add(Genre.Blues, new string[] { "zUuZ3CZYwDc", "fYr819V--ic", "oiyY81VOpBI" });
            SongIDs.Add(Genre.Jazz, new string[] { "YGURYe7coRU", "gz93tWf3TTE", "_sI_Ps7JSEk" });
            SongIDs.Add(Genre.Rock, new string[] { "_wii6Tfb4RQ", "_wii6Tfb4RQ", "O0Az_HYNu4g" });
            SongIDs.Add(Genre.Electro, new string[] { "5uB4HCVD4Ec", "4XpDdIISlYo", "c17k4LfLkaE", "MD_p19guYcs", "Xh2DEUUR7QU" });

            Player1.MediaPlayer.Volume = 80;
            Player1.AutoPlay = false;

            PopulateCommands();

            //init intelligent service
            ApiContainer.Register<FaceService>(new FaceService());
            ApiContainer.Register<ComputerVisionService>(new ComputerVisionService());
            ApiContainer.Register<LuisHelper>(new LuisHelper());

            isListening = false;
           
            //if (timer == null)
            //{
            //    timer = new Timer(OnTimerTick, null, new TimeSpan(0, 0, 1), new TimeSpan(0, APPCONTANTS.IntervalTimerMin, 0));


            //}
        }

        //private async void OnTimerTick(object state)
        //{
        //    //GetPhotoFromCam();
        //}
        /*
        async void GetPhotoFromCam()
        {
            if (!IsWebCamReady) return;

            var photo = await TakePhoto();
            //call computer vision
            if (photo == null) return;

            var result = await ApiContainer.GetApi<ComputerVisionService>().GetImageAnalysis(photo);
            if (result != null)
            {
                var item = new TonyVisionObj();
                if (result.Adult != null)
                {
                    item.adultContent = result.Adult.IsAdultContent.ToString();
                    item.adultScore = result.Adult.AdultScore.ToString();
                }
                else
                {
                    item.adultContent = "False";
                    item.adultScore = "0";
                }

                if (result.Faces != null && result.Faces.Length > 0)
                {
                    int count = 0;
                    item.facesCount = result.Faces.Count();
                    foreach (var face in result.Faces)
                    {
                        count++;
                        if (count > 1)
                        {
                            item.facesDescription += ",";
                        }
                        item.facesDescription += $"[Face : {count}; Age : { face.Age }; Gender : {face.Gender}]";

                    }
                }
                else
                    item.facesCount = 0;



                if (result.Description != null)
                {
                    var Speak = "";
                    foreach (var caption in result.Description.Captions)
                    {
                        Speak += $"[Caption : {caption.Text }; Confidence : {caption.Confidence};],";
                    }
                    string tags = "[Tags : ";
                    foreach (var tag in result.Description.Tags)
                    {
                        tags += tag + ", ";
                    }
                    Speak += tags + "]";
                    item.description = Speak;
                }

                if (result.Tags != null)
                {

                    foreach (var tag in result.Tags)
                    {
                        item.tags += "[ Name : " + tag.Name + "; Confidence : " + tag.Confidence + "; Hint : " + tag.Hint + "], ";
                    }
                }
                var IsUpload = false;
                if (item.description != null)
                {
                    if (item.description.ToLower().Contains("person") || item.description.ToLower().Contains("people"))
                    {
                        IsUpload = true;
                    }
                }
                if (item.tags != null)
                {
                    if (item.tags.ToLower().Contains("man") || item.tags.ToLower().Contains("woman"))
                    {
                        IsUpload = true;
                    }
                }
                if (IsUpload)
                {
                    var uploadRes = await BlobEngine.UploadFile(photo);
                    Debug.WriteLine($"upload : {uploadRes}");
                }
                item.tanggal = DateTime.Now;
                var JsonObj = new StringContent(JsonConvert.SerializeObject(item), Encoding.UTF8, "application/json");
                var res = await httpClient.PostAsync(APPCONTANTS.ApiUrl, JsonObj);
                if (res.IsSuccessStatusCode)
                {
                    Debug.WriteLine("vision captured");

                }


            }
        }*/
        /// <summary>
        /// Triggered when media element used to play synthesized speech messages is loaded.
        /// Initializes SpeechHelper and greets user.
        /// </summary>
        private async void speechMediaElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (speech == null)
            {
                speech = new SpeechHelper(speechMediaElement);

                await speech.Read("tony is ready to serve");
            }
            else
            {
                // Prevents media element from re-greeting visitor
                speechMediaElement.AutoPlay = false;
            }
        }

        void PopulateCommands()
        {
            VoiceCommands = new Dictionary<string, string[]>();
            VoiceCommands.Add(TagCommands.Calling, new string[] { "Hi tony", "Hello tony" });
            VoiceCommands.Add(TagCommands.TurnOnLamp, new string[] { "Please turn on the light" });
            VoiceCommands.Add(TagCommands.TurnOffLamp, new string[] { "Please turn off the light" });
            VoiceCommands.Add(TagCommands.TakePhoto, new string[] { "Take a picture" });
            VoiceCommands.Add(TagCommands.SeeMe, new string[] { "What do you see" });
            VoiceCommands.Add(TagCommands.PlayJazz, new string[] { "Play jazz music" });
            VoiceCommands.Add(TagCommands.PlayBlues, new string[] { "Play blues music" });
            VoiceCommands.Add(TagCommands.PlaySlow, new string[] { "Play pop music" });
            VoiceCommands.Add(TagCommands.PlayRock, new string[] { "Play rock music" });
            VoiceCommands.Add(TagCommands.PlayElectro, new string[] { "Play electro music" });

            VoiceCommands.Add(TagCommands.Thanks, new string[] { "Thank you", "Thanks" });
            VoiceCommands.Add(TagCommands.ReadText, new string[] { "Read this" });
            VoiceCommands.Add(TagCommands.Stop, new string[] { "Stop music", "Stop" });
            VoiceCommands.Add(TagCommands.HowOld, new string[] { "How old is she", "How old is he" });
            VoiceCommands.Add(TagCommands.GetJoke, new string[] { "Tell me some joke" });
            VoiceCommands.Add(TagCommands.ReciteQuran, new string[] { "recite holy verse" });
            VoiceCommands.Add(TagCommands.WhatDate, new string[] { "what date is it" });
            VoiceCommands.Add(TagCommands.WhatTime, new string[] { "what time is it" });
            for (int x = 0; x < Devices.Count; x++)
            {
                VoiceCommands.Add($"TURNON{x}", new string[] { "please turn on " + Devices[x].Name });
                VoiceCommands.Add($"TURNOFF{x}", new string[] { "please turn off " + Devices[x].Name });
            }
        }
        
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {

            // Keep track of the UI thread dispatcher, as speech events will come in on a separate thread.
            dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;

            // Prompt the user for permission to access the microphone. This request will only happen
            // once, it will not re-prompt if the user rejects the permission.
            bool permissionGained = await AudioCapturePermissions.RequestMicrophonePermission();
            if (permissionGained)
            {

                // Initialize resource map to retrieve localized speech strings.
                Language speechLanguage = SpeechRecognizer.SystemSpeechLanguage;
                string langTag = speechLanguage.LanguageTag;
                speechContext = ResourceContext.GetForCurrentView();
                speechContext.Languages = new string[] { langTag };

                speechResourceMap = ResourceManager.Current.MainResourceMap.GetSubtree("LocalizationSpeechResources");

                PopulateLanguageDropdown();
                await InitializeRecognizer(SpeechRecognizer.SystemSpeechLanguage);

                TurnRecognizer1();
            }
            else
            {
                this.resultTextBlock.Visibility = Visibility.Visible;
                this.resultTextBlock.Text = "Permission to access capture resources was not given by the user, reset the application setting in Settings->Privacy->Microphone.";
                cbLanguageSelection.IsEnabled = false;
                //luis
            }

        }
        /// <summary>
        /// Look up the supported languages for this speech recognition scenario, 
        /// that are installed on this machine, and populate a dropdown with a list.
        /// </summary>
        private void PopulateLanguageDropdown()
        {
            // disable the callback so we don't accidentally trigger initialization of the recognizer
            // while initialization is already in progress.
            cbLanguageSelection.SelectionChanged -= cbLanguageSelection_SelectionChanged;
            Language defaultLanguage = SpeechRecognizer.SystemSpeechLanguage;
            IEnumerable<Language> supportedLanguages = SpeechRecognizer.SupportedGrammarLanguages;
            foreach (Language lang in supportedLanguages)
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Tag = lang;
                item.Content = lang.DisplayName;

                cbLanguageSelection.Items.Add(item);
                if (lang.LanguageTag == defaultLanguage.LanguageTag)
                {
                    item.IsSelected = true;
                    cbLanguageSelection.SelectedItem = item;
                }
            }

            cbLanguageSelection.SelectionChanged += cbLanguageSelection_SelectionChanged;
        }

        /// <summary>
        /// When a user changes the speech recognition language, trigger re-initialization of the 
        /// speech engine with that language, and change any speech-specific UI assets.
        /// </summary>
        /// <param name="sender">Ignored</param>
        /// <param name="e">Ignored</param>
        private async void cbLanguageSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem item = (ComboBoxItem)(cbLanguageSelection.SelectedItem);
            Language newLanguage = (Language)item.Tag;
            if (speechRecognizer != null)
            {
                if (speechRecognizer.CurrentLanguage == newLanguage)
                {
                    return;
                }
            }

            // trigger cleanup and re-initialization of speech.
            try
            {
                // update the context for resource lookup
                speechContext.Languages = new string[] { newLanguage.LanguageTag };

                await InitializeRecognizer(newLanguage);
            }
            catch (Exception exception)
            {
                var messageDialog = new Windows.UI.Popups.MessageDialog(exception.Message, "Exception");
                await messageDialog.ShowAsync();
            }
        }

        /// <summary>
        /// Initialize Speech Recognizer and compile constraints.
        /// </summary>
        /// <param name="recognizerLanguage">Language to use for the speech recognizer</param>
        /// <returns>Awaitable task.</returns>
        private async Task InitializeRecognizer(Language recognizerLanguage)
        {
            if (speechRecognizer != null)
            {
                // cleanup prior to re-initializing this scenario.
                speechRecognizer.StateChanged -= SpeechRecognizer_StateChanged;
                speechRecognizer.ContinuousRecognitionSession.Completed -= ContinuousRecognitionSession_Completed;
                speechRecognizer.ContinuousRecognitionSession.ResultGenerated -= ContinuousRecognitionSession_ResultGenerated;

                this.speechRecognizer.Dispose();
                this.speechRecognizer = null;
            }

            try
            {
                this.speechRecognizer = new SpeechRecognizer(recognizerLanguage);

                // Provide feedback to the user about the state of the recognizer. This can be used to provide visual feedback in the form
                // of an audio indicator to help the user understand whether they're being heard.
                speechRecognizer.StateChanged += SpeechRecognizer_StateChanged;

                // Build a command-list grammar. Commands should ideally be drawn from a resource file for localization, and 
                // be grouped into tags for alternate forms of the same command.
                foreach (var item in VoiceCommands)
                {
                    speechRecognizer.Constraints.Add(
                   new SpeechRecognitionListConstraint(
                      item.Value, item.Key));
                }


                // Update the help text in the UI to show localized examples
                string uiOptionsText = string.Format("Try saying '{0}', '{1}' or '{2}'",
                    VoiceCommands[TagCommands.Calling][0],
                    VoiceCommands[TagCommands.TakePhoto][0],
                    VoiceCommands[TagCommands.TurnOnLamp][0]);
                /*
                listGrammarHelpText.Text = string.Format("{0}\n{1}",
                    speechResourceMap.GetValue("ListGrammarHelpText", speechContext).ValueAsString,
                    uiOptionsText);
                    */
                SpeechRecognitionCompilationResult result = await speechRecognizer.CompileConstraintsAsync();
                if (result.Status != SpeechRecognitionResultStatus.Success)
                {
                    // Disable the recognition buttons.
                    //btnContinuousRecognize.IsEnabled = false;
                    IsRecognizerEnable = false;
                    // Let the user know that the grammar didn't compile properly.
                    resultTextBlock.Visibility = Visibility.Visible;
                    resultTextBlock.Text = "Unable to compile grammar.";
                }
                else
                {
                    //btnContinuousRecognize.IsEnabled = true;

                    resultTextBlock.Visibility = Visibility.Collapsed;
                    IsRecognizerEnable = true;

                    // Handle continuous recognition events. Completed fires when various error states occur. ResultGenerated fires when
                    // some recognized phrases occur, or the garbage rule is hit.
                    speechRecognizer.ContinuousRecognitionSession.Completed += ContinuousRecognitionSession_Completed;
                    speechRecognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated;
                }
            }
            catch (Exception ex)
            {
                if ((uint)ex.HResult == HResultRecognizerNotFound)
                {
                    //btnContinuousRecognize.IsEnabled = false;
                    IsRecognizerEnable = false;
                    resultTextBlock.Visibility = Visibility.Visible;
                    resultTextBlock.Text = "Speech Language pack for selected language not installed.";
                }
                else
                {
                    var messageDialog = new Windows.UI.Popups.MessageDialog(ex.Message, "Exception");
                    await messageDialog.ShowAsync();
                }
            }

        }

        

        /// <summary>
        /// Handle events fired when error conditions occur, such as the microphone becoming unavailable, or if
        /// some transient issues occur.
        /// </summary>
        /// <param name="sender">The continuous recognition session</param>
        /// <param name="args">The state of the recognizer</param>
        private async void ContinuousRecognitionSession_Completed(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionCompletedEventArgs args)
        {
            if (args.Status != SpeechRecognitionResultStatus.Success)
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    this.NotifyUser("Continuous Recognition Completed: " + args.Status.ToString(), NotifyType.StatusMessage);

                    cbLanguageSelection.IsEnabled = true;
                    isListening = false;
                });
            }
        }

        /// <summary>
        /// Handle events fired when a result is generated. This may include a garbage rule that fires when general room noise
        /// or side-talk is captured (this will have a confidence of Rejected typically, but may occasionally match a rule with
        /// low confidence).
        /// </summary>
        /// <param name="sender">The Recognition session that generated this result</param>
        /// <param name="args">Details about the recognized speech</param>
        private async void ContinuousRecognitionSession_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            // The garbage rule will not have a tag associated with it, the other rules will return a string matching the tag provided
            // when generating the grammar.
            string tag = "unknown";
            if (args.Result.Constraint != null)
            {
                tag = args.Result.Constraint.Tag;
            }

            // Developers may decide to use per-phrase confidence levels in order to tune the behavior of their 
            // grammar based on testing.
            if (args.Result.Confidence == SpeechRecognitionConfidence.Low ||
                args.Result.Confidence == SpeechRecognitionConfidence.Medium ||
                args.Result.Confidence == SpeechRecognitionConfidence.High)
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    heardYouSayTextBlock.Visibility = Visibility.Visible;
                    resultTextBlock.Visibility = Visibility.Visible;
                    resultTextBlock.Text = string.Format("Heard: '{0}', (Tag: '{1}', Confidence: {2})", args.Result.Text, tag, args.Result.Confidence.ToString());
                    switch (tag)
                    {

                        case TagCommands.GetJoke:
                            {

                                var res = await JokeHelper.GetJoke();
                                if (!string.IsNullOrEmpty(res.value.joke))
                                {
                                    await speech.Read(res.value.joke);
                                    resultTextBlock.Text = res.value.joke;
                                }
                            }
                            break;

                        case TagCommands.HowOld:
                            {
                                var photo = await TakePhoto();
                                //call computer vision
                                var faces = await ApiContainer.GetApi<FaceService>().UploadAndDetectFaceAttributes(photo);
                                var res = ApiContainer.GetApi<FaceService>().HowOld(faces);
                                if (!string.IsNullOrEmpty(res))
                                {
                                    await speech.Read(res);
                                    resultTextBlock.Text = res;
                                }
                            }
                            break;
                        case TagCommands.Calling:
                            await speech.Read("Yes, what can I do Boss?");
                            break;
                        case TagCommands.SeeMe:
                            {
                                var photo = await TakePhoto();
                                //call computer vision
                                var res = await ApiContainer.GetApi<ComputerVisionService>().RecognizeImage(photo);
                                if (!string.IsNullOrEmpty(res))
                                {
                                    await speech.Read(res);
                                    resultTextBlock.Text = "I see " + res;
                                }
                            }
                            break;
                        case TagCommands.ReadText:
                            {
                                var photo = await TakePhoto();
                                //call computer vision
                                var res = await ApiContainer.GetApi<ComputerVisionService>().RecognizeText(photo);
                                if (!string.IsNullOrEmpty(res))
                                {
                                    await speech.Read(res);
                                    resultTextBlock.Text = "read: " + res;
                                }
                            }
                            break;
                        case TagCommands.Stop:
                            Player1.MediaPlayer.Pause();
                            break;
                        case TagCommands.PlayBlues:
                        case TagCommands.PlaySlow:
                        case TagCommands.PlayRock:
                        case TagCommands.PlayJazz:
                        case TagCommands.PlayElectro:

                            {
                                
                                var genre = Genre.Slow;
                                switch (tag)
                                {
                                    case TagCommands.PlayBlues: genre = Genre.Blues; break;
                                    case TagCommands.PlayRock: genre = Genre.Rock; break;
                                    case TagCommands.PlaySlow: genre = Genre.Slow; break;
                                    case TagCommands.PlayJazz: genre = Genre.Jazz; break;
                                    case TagCommands.PlayElectro: genre = Genre.Electro; break;
                                }
                                var rnd = new Random(Environment.TickCount);
                                var selIds = SongIDs[genre];
                                var num = rnd.Next(0, selIds.Length - 1);
                                var url = await YouTube.GetVideoUriAsync(selIds[num], YouTubeQuality.QualityLow);
                                MediaPlayerHelper.CleanUpMediaPlayerSource(Player1.MediaPlayer);
                                Player1.MediaPlayer.Source = new MediaItem(url.Uri.ToString()).MediaPlaybackItem;
                                Player1.MediaPlayer.Play();
                            }

                            break;

                        case TagCommands.TakePhoto:
                            await speech.Read("I will take your picture boss");
                            //GetPhotoFromCam();
                            break;
                        case TagCommands.Thanks:
                            await speech.Read("My pleasure boss");
                            break;
                        case TagCommands.TurnOnLamp:
                            {
                                //await speech.Read("Turn on the light");
                                //var Pesan = Encoding.UTF8.GetBytes("LIGHT_ON");
                                //clientMqtt.PublishMessage(Pesan);
                            }
                            break;
                        case TagCommands.TurnOffLamp:
                            {
                                //await speech.Read("Turn off the light");
                                //var Pesan = Encoding.UTF8.GetBytes("LIGHT_OFF");
                                //clientMqtt.Publish( Pesan);
                            }
                            break;
                        case TagCommands.ReciteQuran:
                            {
                                try
                                {
                                    Random rnd = new Random(Environment.TickCount);
                                    var surah = rnd.Next(1, 114);
                                    var rslt = await httpClient.GetAsync($"http://qurandataapi.azurewebsites.net/api/Ayah/GetAyahCountBySurah?Surah={surah}");
                                    var ayah = int.Parse(await rslt.Content.ReadAsStringAsync());
                                    ayah = rnd.Next(1, ayah);
                                    rslt = await httpClient.GetAsync($"http://qurandataapi.azurewebsites.net/api/Ayah/GetMediaByAyah?Surah={surah}&Ayah={ayah}&ReciterId=11");
                                    var media = JsonConvert.DeserializeObject<QuranMedia>(await rslt.Content.ReadAsStringAsync());
                                    if (media != null)
                                    {
                                        MediaPlayerHelper.CleanUpMediaPlayerSource(Player1.MediaPlayer);
                                        Player1.MediaPlayer.Source = new MediaItem(media.Url).MediaPlaybackItem;
                                        Player1.MediaPlayer.Play();

                                    }
                                }
                                catch
                                {
                                    await speech.Read("there is problem on the service");
                                }
                            }
                            break;
                        case TagCommands.WhatDate: { await speech.Read("Today is " + DateTime.Now.ToString("dd MMMM yyyy")); }; break;
                        case TagCommands.WhatTime: { await speech.Read("Current time is " + DateTime.Now.ToString("HH:mm")); }; break;
                        default:
                            for (int x = 0; x < Devices.Count; x++)
                            {
                                if (tag == $"TURNON{x}")
                                {
                                    SwitchDevice(true, Devices[x].IP);
                                    break;
                                }
                                else if (tag == $"TURNOFF{x}")
                                {
                                    SwitchDevice(false, Devices[x].IP);
                                    break;
                                }
                            }
                            break;
                    }
                });
            }
            else
            {
                // In some scenarios, a developer may choose to ignore giving the user feedback in this case, if speech
                // is not the primary input mechanism for the application.
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    heardYouSayTextBlock.Visibility = Visibility.Collapsed;
                    resultTextBlock.Visibility = Visibility.Visible;
                    resultTextBlock.Text = string.Format("Sorry, I didn't catch that. (Heard: '{0}', Tag: {1}, Confidence: {2})", args.Result.Text, tag, args.Result.Confidence.ToString());
                });
            }
        }
        private void Player1_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            Debug.WriteLine(e.ErrorMessage);
        }

        private async Task<StorageFile> TakePhoto()
        {

            // Confirms that webcam has been properly initialized and oxford is ready to go
            if (webcam.IsInitialized())
            {

                try
                {
                    // Stores current frame from webcam feed in a temporary folder
                    StorageFile image = await webcam.CapturePhoto();
                    return image;
                }
                catch
                {
                    // General error. This can happen if there are no visitors authorized in the whitelist
                    Debug.WriteLine("WARNING: Oxford just threw a general expception.");
                }

            }
            else
            {
                if (!webcam.IsInitialized())
                {
                    // The webcam has not been fully initialized for whatever reason:
                    Debug.WriteLine("Unable to analyze visitor at door as the camera failed to initlialize properly.");
                    await speech.Read("No camera available");
                }
                /*
                if (!initializedOxford)
                {
                    // Oxford is still initializing:
                    Debug.WriteLine("Unable to analyze visitor at door as Oxford Facial Recogntion is still initializing.");
                }*/
            }
            return null;
        }

        /// <summary>
        /// Provide feedback to the user based on whether the recognizer is receiving their voice input.
        /// </summary>
        /// <param name="sender">The recognizer that is currently running.</param>
        /// <param name="args">The current state of the recognizer.</param>
        private async void SpeechRecognizer_StateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                this.NotifyUser(args.State.ToString(), NotifyType.StatusMessage);
            });
        }


        async void TurnRecognizer1()
        {
            if (isListening == false)
            {
                // The recognizer can only start listening in a continuous fashion if the recognizer is currently idle.
                // This prevents an exception from occurring.
                if (speechRecognizer.State == SpeechRecognizerState.Idle)
                {
                    try
                    {
                        await speechRecognizer.ContinuousRecognitionSession.StartAsync();

                        cbLanguageSelection.IsEnabled = false;
                        isListening = true;
                    }
                    catch (Exception ex)
                    {
                        var messageDialog = new Windows.UI.Popups.MessageDialog(ex.Message, "Exception");
                        await messageDialog.ShowAsync();
                    }
                }
            }
            else
            {
                isListening = false;

                cbLanguageSelection.IsEnabled = true;

                heardYouSayTextBlock.Visibility = Visibility.Collapsed;
                resultTextBlock.Visibility = Visibility.Collapsed;
                if (speechRecognizer.State != SpeechRecognizerState.Idle)
                {
                    try
                    {
                        // Cancelling recognition prevents any currently recognized speech from
                        // generating a ResultGenerated event. StopAsync() will allow the final session to 
                        // complete.
                        await speechRecognizer.ContinuousRecognitionSession.CancelAsync();
                    }
                    catch (Exception ex)
                    {
                        var messageDialog = new Windows.UI.Popups.MessageDialog(ex.Message, "Exception");
                        await messageDialog.ShowAsync();
                    }
                }
            }
        }
        public void NotifyUser(string strMessage, NotifyType type)
        {
            switch (type)
            {
                case NotifyType.StatusMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.ErrorMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }
            StatusBlock2.Text = strMessage;

            // Collapse the StatusBlock2 if it has no text to conserve real estate.
            StatusBorder.Visibility = (StatusBlock2.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
            if (StatusBlock2.Text != String.Empty)
            {
                StatusBorder.Visibility = Visibility.Visible;
                StatusPanel.Visibility = Visibility.Visible;
            }
            else
            {
                StatusBorder.Visibility = Visibility.Collapsed;
                StatusPanel.Visibility = Visibility.Collapsed;
            }
        }
        public enum NotifyType
        {
            StatusMessage,
            ErrorMessage
        };
        private async void WebcamFeed_Loaded(object sender, RoutedEventArgs e)
        {
            if (webcam == null || !webcam.IsInitialized())
            {
                // Initialize Webcam Helper
                webcam = new WebcamHelper();
                await webcam.InitializeCameraAsync();
                
                // Set source of WebcamFeed on MainPage.xaml
                WebcamFeed.Source = webcam.mediaCapture;
                IsWebCamReady = true;
                // Check to make sure MediaCapture isn't null before attempting to start preview. Will be null if no camera is attached.

                if (WebcamFeed.Source != null)
                {
                    // Start the live feed
                    await webcam.StartCameraPreview();

                }
            }
            else if (webcam.IsInitialized())
            {
                WebcamFeed.Source = webcam.mediaCapture;
                IsWebCamReady = true;
                // Check to make sure MediaCapture isn't null before attempting to start preview. Will be null if no camera is attached.

                if (WebcamFeed.Source != null)
                {
                    await webcam.StartCameraPreview();
                }
            }
        }
        #endregion
        private void Control_Device(object sender, RoutedEventArgs e)
        {
            var btn = (sender as Button);
            var item = btn.DataContext as DeviceData;
            if (item != null)
            {
                var state = btn.Tag.ToString() == "On" ? true : false;
                SwitchDevice(state, item.IP);
            }
        }
        private async void SwitchDevice(bool State, string IP)
        {
            if (State)
            {
                //string DeviceID = $"Device{btn.CommandArgument}IP";
                string URL = $"http://{IP}/cm?cmnd=Power%20On";
                await clientMqtt.InvokeMethod("BMCSecurityBot", "OpenURL", new string[] { URL });
            }
            else
            {
                //string DeviceID = $"Device{btn.CommandArgument}IP";
                string URL = $"http://{IP}/cm?cmnd=Power%20Off";
                await clientMqtt.InvokeMethod("BMCSecurityBot", "OpenURL", new string[] { URL });
            }
        }

        private async void TabView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (webcam != null && IsWebCamReady)
            {
                await webcam.StopCameraPreview();
                IsWebCamReady = false;
            }
        }
    }

    public class DeviceData
    {
        public string IP { get; set; }
        public string Name { get; set; }
        public int ID { get; set; }

        public static List<DeviceData> GetAllDevices()
        {
            return new List<DeviceData>()
            {
                new DeviceData (){ ID=1, Name="Toilet Lamp", IP="192.168.1.27" },
                new DeviceData (){ ID=2, Name="Printer Room Lamp", IP="192.168.1.25" },
                new DeviceData (){ ID=3, Name="Living Room Lamp", IP="192.168.1.28" },
                new DeviceData (){ ID=4, Name="Prayer Room Fan", IP="192.168.1.31" },
                new DeviceData (){ ID=5, Name="Printer Fan", IP="192.168.1.32" },
                new DeviceData (){ ID=6, Name="Kitchen Fan", IP="192.168.1.33" },
                 //new DeviceData (){ ID=7, Name="Prayer Room", IP="192.168.1.35" },
                  new DeviceData (){ ID=8, Name="Guest Room Lamp", IP="192.168.1.26" },
                     new DeviceData (){ ID=7, Name="Front Room Fan", IP="192.168.1.36" },
                  new DeviceData (){ ID=8, Name="Prayer Room Lamp", IP="192.168.1.29" }

            };
        }
        public static List<DeviceData> FilterDevice(string Keyword)
        {
            var datas = from x in GetAllDevices()
                        where x.Name.Contains(Keyword, StringComparison.InvariantCultureIgnoreCase)
                        select x;
            return datas.ToList();
        }

    }
    public class MqttService
    {
        public MqttService()
        {
            SetupMqtt();
        }
        MqttClient MqttClient;
        const string DataTopic = "bmc/homeautomation/data";
        const string ControlTopic = "bmc/homeautomation/control";
        public void PublishMessage(string Message)
        {
            MqttClient.Publish(DataTopic, Encoding.UTF8.GetBytes(Message), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
        }
        public void SendCommand(string Message, string Topic)
        {
            MqttClient.Publish(Topic, Encoding.UTF8.GetBytes(Message), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
        }
        void SetupMqtt()
        {
            string IPBrokerAddress = APPCONTANTS.MQTT_SERVER;
            string ClientUser = APPCONTANTS.MQTT_USER;
            string ClientPass = APPCONTANTS.MQTT_PASS;

            MqttClient = new MqttClient(IPBrokerAddress);

            // register a callback-function (we have to implement, see below) which is called by the library when a message was received
            MqttClient.MqttMsgPublishReceived += client_MqttMsgPublishReceived;

            // use a unique id as client id, each time we start the application
            var clientId = "bmc-tony-app";//Guid.NewGuid().ToString();

            MqttClient.Connect(clientId, ClientUser, ClientPass);
            Console.WriteLine("MQTT is connected");
        } // this code runs when a message was received
        void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string ReceivedMessage = Encoding.UTF8.GetString(e.Message);
            if (e.Topic == ControlTopic)
            {
                Console.WriteLine(ReceivedMessage);
            }
        }
        // Invoke the direct method on the device, passing the payload
        public Task InvokeMethod(string DeviceId, string ActionName, params string[] Params)
        {
            return Task.Factory.StartNew(() =>
            {
                var action = new DeviceAction() { ActionName = ActionName, Params = Params };
                SendCommand(JsonConvert.SerializeObject(action), ControlTopic);
            });
            //Console.WriteLine("Response status: {0}, payload:", response.Status);
            //Console.WriteLine(response.GetPayloadAsJson());
        }

        public Task InvokeMethod2(string Topic, string ActionName, params string[] Params)
        {
            return Task.Factory.StartNew(() =>
            {
                var action = new DeviceAction() { ActionName = ActionName, Params = Params };
                SendCommand(JsonConvert.SerializeObject(action), Topic);
            });

            //Console.WriteLine("Response status: {0}, payload:", response.Status);
            //Console.WriteLine(response.GetPayloadAsJson());
        }

    }
    public class DeviceAction
    {
        public string ActionName { get; set; }
        public string[] Params { get; set; }
    }
    #region Supporting Class

    public class tonyVisionObj
    {
        public int id { get; set; }
        public string description { get; set; }
        public DateTime tanggal { get; set; }
        public string tags { get; set; }
        public string adultContent { get; set; }
        public string adultScore { get; set; }
        public int facesCount { get; set; }
        public string facesDescription { get; set; }
    }

    public class ImageItem
    {
        public object ImageSource { get; set; }
    }
    public class MediaItem
    {
        public MediaPlaybackItem MediaPlaybackItem { get; private set; }
        public Uri Uri { set; get; }
        public MediaItem(string Url)
        {
            Uri = new Uri(Url);
            MediaPlaybackItem = new MediaPlaybackItem(MediaSource.CreateFromUri(Uri));
        }
    }

    public class QuranMedia
    {
        public string Url { get; set; }
        public string FileName { get; set; }
    }

    #endregion
}
