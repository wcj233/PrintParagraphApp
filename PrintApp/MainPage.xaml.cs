using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Printing;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Printing;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace PrintApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>

    public class MainPageViewModel {
        private string selectedQuote;
        private string message;

        private List<string> trekkieQuotes = new List<string>()
            {
                "Open a hailing frequency.",
                "The best diplomat that I know is a fully-loaded phaser bank.",
                "In four hours the ship blows up.",
                "Phasers on stun.",
                "Please, Captain, not in front of the Klingons.",
                "Worlds are conquered, galaxies destroyed...but a woman is always a woman.",
                "We have them just where they want us. "
            };

        public MainPageViewModel()
        {
            this.SelectedQuote = this.trekkieQuotes.First();
        }

        /// <summary>
        /// Warning, Error, Info message
        /// </summary>
        public string Message
        {
            get { return this.message; }
            set { this.message=value; }
        }

        public List<string> TrekkieQuotes
        {
            get { return this.trekkieQuotes; }
        }

        public string SelectedQuote
        {
            get { return selectedQuote; }
            set { this.selectedQuote= value; }
        }
    }

    public class PrintServiceEventArgs : EventArgs
    {
        public PrintServiceEventArgs()
        { }

        public PrintServiceEventArgs(string message)
        {
            this.Message = message;
        }

        /// <summary>
        /// The message from the print service.
        /// </summary>
        public string Message { get; set; }
    }


    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            this.RegisterForPrinting(this, typeof(MainPrintPage), this.DataContext);
        }

        /// <summary>
        /// Event handler for print button click.
        /// </summary>
        private async void Print_Click(object sender, RoutedEventArgs e)
        {
            await Windows.Graphics.Printing.PrintManager.ShowPrintUIAsync();
        }

        private int pageNumber;
        private Page callingPage { get; set; }
        private Type printPageType { get; set; }
        private PrintDocument printDocument { get; set; }
        private IPrintDocumentSource printDocumentSource { get; set; }
        public event EventHandler<PrintServiceEventArgs> OnStatusChanged;
        private List<UIElement> printPreviewPages = new List<UIElement>();
        private double HorizontalPrintMargin = 0.075;
        private double VerticalPrintMargin = 0.03;

        public void RegisterForPrinting(Page sourcePage, Type printPageType, object viewModel)
        {
            this.callingPage = sourcePage;

            if (PrintingRoot == null)
            {
                //this.OnStatusChanged(new PrintServiceEventArgs("The calling page has no PrintingRoot Canvas."));
                return;
            }

            this.printPageType = printPageType;
            this.DataContext = viewModel;

            // Prep the content
            this.PreparePrintContent();

            // Create the PrintDocument.
            printDocument = new PrintDocument();

            // Save the DocumentSource.
            printDocumentSource = printDocument.DocumentSource;

            // Add an event handler which creates preview pages.
            printDocument.Paginate += PrintDocument_Paginate;

            // Add an event handler which provides a specified preview page.
            printDocument.GetPreviewPage += PrintDocument_GetPrintPreviewPage;

            // Add an event handler which provides all final print pages.
            printDocument.AddPages += PrintDocument_AddPages;

            // Create a PrintManager and add a handler for printing initialization.
            PrintManager printMan = PrintManager.GetForCurrentView();

            try
            {
                printMan.PrintTaskRequested += PrintManager_PrintTaskRequested;
                //this.OnStatusChanged(new PrintServiceEventArgs("Registered successfully."));
            }
            catch (InvalidOperationException)
            {
                // Probably already registered.
                //this.OnStatusChanged(new PrintServiceEventArgs("You were already registered."));
            }
        }

        private void PrintManager_PrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs e)
        {
            PrintTask printTask = null;
            printTask = e.Request.CreatePrintTask("U2U Consult MVVM XAML Printing Job.", sourceRequested =>
            {
                // Print Task event handler is invoked when the print job is completed.
                printTask.Completed += async (s, args) =>
                {
                    // Notify the user when the print operation fails.
                    if (args.Completion == PrintTaskCompletion.Failed)
                    {
                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            string a = "";
                            //this.OnStatusChanged(new PrintServiceEventArgs("Sorry, failed to print."));
                        });
                    }
                };

                sourceRequested.SetSource(printDocumentSource);
            });
        }

        private void PrintDocument_AddPages(object sender, AddPagesEventArgs e)
        {
            for (int i = 0; i < printPreviewPages.Count; i++)
            {
                // We should have all pages ready at this point...
                printDocument.AddPage(printPreviewPages[i]);
            }

            PrintDocument printDoc = (PrintDocument)sender;

            // Indicate that all of the print pages have been provided
            printDoc.AddPagesComplete();
        }

        private void PrintDocument_GetPrintPreviewPage(object sender, GetPreviewPageEventArgs e)
        {

            PrintDocument printDoc = (PrintDocument)sender;

            printDoc.SetPreviewPage(e.PageNumber, printPreviewPages[e.PageNumber - 1]);
        }

        private void PrintDocument_Paginate(object sender, PaginateEventArgs e)
        {
            // Clear the cache of preview pages 
            printPreviewPages.Clear();
            this.pageNumber = 0;

            // Clear the printing root of preview pages
            PrintingRoot.Children.Clear();

            for (int i = 0; i < NeedToPrintPages.Count; i++) {

                // This variable keeps track of the last RichTextBlockOverflow element that was added to a page which will be printed
                RichTextBlockOverflow lastRTBOOnPage;

                // Get the PrintTaskOptions
                PrintTaskOptions printingOptions = ((PrintTaskOptions)e.PrintTaskOptions);

                // Get the page description to deterimine how big the page is
                PrintPageDescription pageDescription = printingOptions.GetPageDescription(0);

                // We know there is at least one page to be printed. passing null as the first parameter to
                // AddOnePrintPreviewPage tells the function to add the first page.
                lastRTBOOnPage = AddOnePrintPreviewPage(null, pageDescription,i);

                // We know there are more pages to be added as long as the last RichTextBoxOverflow added to a print preview
                // page has extra content
                while (lastRTBOOnPage.HasOverflowContent && lastRTBOOnPage.Visibility == Windows.UI.Xaml.Visibility.Visible)
                {
                    lastRTBOOnPage = AddOnePrintPreviewPage(lastRTBOOnPage, pageDescription,i);
                }

            }



                

            PrintDocument printDoc = (PrintDocument)sender;

            // Report the number of preview pages created
            printDoc.SetPreviewPageCount(printPreviewPages.Count, PreviewPageCountType.Intermediate);
        }

        //private PrintPage firstPage { get; set; }

        private ObservableCollection<Page> NeedToPrintPages = new ObservableCollection<Page>();
        private void PreparePrintContent()
        {
            PrintingRoot.Children.Clear();
            // Create and populate print page.
            var printPage = Activator.CreateInstance(this.printPageType) as Page;

            printPage.DataContext = this.DataContext;

            var printPageRtb = printPage.Content as RichTextBlock;
            while (printPageRtb.Blocks.Count > 0)
            {
                PrintPage firstPage = new PrintPage();
                firstPage.AddContent(new Paragraph());
                var paragraph = printPageRtb.Blocks.First() as Paragraph;
                printPageRtb.Blocks.Remove(paragraph);
                
                
                firstPage.AddContent(paragraph);
                NeedToPrintPages.Add(firstPage);
                PrintingRoot.Children.Add(firstPage);
            };

            // Send it to the printing root.
            
            
            
        }

        private RichTextBlockOverflow AddOnePrintPreviewPage(RichTextBlockOverflow lastRTBOAdded, PrintPageDescription printPageDescription,int index)
        {
            // XAML element that is used to represent to "printing page"
            FrameworkElement page;

            // The link container for text overflowing in this page
            RichTextBlockOverflow textLink;

            // Check if this is the first page (no previous RichTextBlockOverflow)
            if (lastRTBOAdded == null)
            {
                // If this is the first page add the specific scenario content
                page = NeedToPrintPages[index];
            }
            else
            {
                // Flow content (text) from previous pages
                page = new PrintPage(lastRTBOAdded);

                // Remove the duplicate OverflowContentTarget. 
                ((RichTextBlock)page.FindName("textContent")).OverflowContentTarget = null;
            }

            // Set paper width
            page.Width = printPageDescription.PageSize.Width;
            page.Height = printPageDescription.PageSize.Height;

            Grid printableArea = (Grid)page.FindName("printableArea");

            // Get the margins size
            // If the ImageableRect is smaller than the app provided margins use the ImageableRect
            double marginWidth = Math.Max(printPageDescription.PageSize.Width - printPageDescription.ImageableRect.Width, printPageDescription.PageSize.Width * HorizontalPrintMargin * 2);
            double marginHeight = Math.Max(printPageDescription.PageSize.Height - printPageDescription.ImageableRect.Height, printPageDescription.PageSize.Height * VerticalPrintMargin * 2);

            // Set-up "printable area" on the "paper"
            printableArea.Width = page.Width - marginWidth;
            printableArea.Height = page.Height - marginHeight;

            // Add the (newly created) page to the printing root which is part of the visual tree and force it to go
            // through layout so that the linked containers correctly distribute the content inside them.            
            PrintingRoot.Children.Add(page);
            PrintingRoot.InvalidateMeasure();
            PrintingRoot.UpdateLayout();

            // Find the last text container and see if the content is overflowing
            textLink = (RichTextBlockOverflow)page.FindName("continuationPageLinkedContainer");

            // Add page number
            this.pageNumber += 1;
            TextBlock pageNumberTextBlock = (TextBlock)page.FindName("pageNumber");
            if (pageNumberTextBlock != null)
            {
                pageNumberTextBlock.Text = string.Format("- {0} -", this.pageNumber);
            }

            // Add the page to the page preview collection
            printPreviewPages.Add(page);

            return textLink;
        }

    }
}
