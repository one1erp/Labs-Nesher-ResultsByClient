using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Common;
using DAL;
using LSExtensionExplorer;
using LSSERVICEPROVIDERLib;
using Microsoft.Office.Interop.Excel;
using Telerik.Charting;
using Telerik.WinControls;
using Telerik.WinControls.UI;


namespace ResultsByClient
{


    public partial class ResultsByClientCls : UserControl, ILSXplVisualControl
    {

        #region members


        private INautilusDBConnection ntlCon;


        private NautilusServiceProvider serviceProvider;
        private Client client;
        private IDataLayer dal;
        private RadListDataItem seleDataItem;
        private IEnumerable productsByClient;
        private SpecificationGrade currentSpecificationGrade;
        private Specification currentSpec;
        private List<Specification> specifications;
        private List<Grade> sg;
        public bool DEBUG;
        private const string MAIN_SERIES = "MAIN_SERIES";

        #endregion

        #region ctor

        public ResultsByClientCls()
        {
            InitializeComponent();
            dtFrom.Value = DateTime.Now.AddDays(-300);
            dtTo.Value = DateTime.Now.AddDays(1);
        }
        #endregion

        #region ILSXplVisualControl
        public void PreDisplay()
        {
            //    radChartView1.LabelFormatting += radChartView1_LabelFormatting;
            ExceptionThrown += LSExtensionExpl_ExceptionThrown;
            //throw new NotImplementedException();
            Utils.CreateConstring(ntlCon);
            if (DEBUG)
                dal = new MockDataLayer();
            else
                dal = new DataLayer();
            dal.Connect();

            var q=dal.GetTestTemplatesForPriceList();
       
            var rt = dal.GetAll<ResultTemplate>();
            var s = from item in rt
                    where item.RESULT_TYPE == "N"
                          && item.CLASSIFICATION == "Chart"
                    orderby item.Name
                    select item;

            //  ddlResultTemplates.SelectedIndexChanged += DdlResultTemplatesSelectedIndexChanged;
            ddlResultTemplates.DisplayMember = "Name";
            ddlResultTemplates.ValueMember = "ResultTemplateId";
            ddlResultTemplates.DataSource = s;



        }

        public void ChangeDataExplorerView(DataExplorerViewStyles style)
        {
            //throw new NotImplementedException();
        }

        public string GetObjectsStaticItemText()
        {
            return client.Name;
        }

        public void BeforeFocusedNodeChange(string keyData)
        {

            numericUpDown1.Value = 0;
            trackBar1.Value = 0;
            radChartView1.Series.Clear();
            radChartView1.Axes.Clear();
        }

        public void FocusedNodeChanged(string keyData)
        {
            var id = keyData.Split('/')[0];
            GetInitData(id);

        }

        public void NeedRefresh(string keyData, params string[] parameters)
        {
            seleDataItem = ddlProducts.SelectedItem;
            var id = keyData.Split('/')[0];
            GetInitData(id);
            ddlProducts.SelectedIndex = ddlProducts.DropDownListElement.FindStringExact(seleDataItem.Text);
            GetData_Click(null, null);
        }

        public void ProcessToolbarItemClick(ToolbarItem item)
        {
            //throw new NotImplementedException();
        }

        public void DataExplorerToolbarButtonClicked(ToolbarItem item)
        {
            //throw new NotImplementedException();
        }

        public void SetServiceProvider(object sp)
        {

            if (sp != null)
            {
                // Cast the object to the correct type
                serviceProvider = (NautilusServiceProvider)sp;

                // Using the service provider object get the XML Processor interface
                // We will use this object to get information from the database
                //     nautilusProcessXML = Utils.GetXmlProcessor(serviceProvider);


                ntlCon = Utils.GetNtlsCon(serviceProvider);
            }

        }

        public void InitializeToolbarItemsStatus(ref Hashtable toolbarItems)
        {
            //throw new NotImplementedException();
        }

        public event ExceptionThrownEventHandler ExceptionThrown;
        void LSExtensionExpl_ExceptionThrown(object sender, Exception e)
        {
            MessageBox.Show("Error");
        }
        #endregion

        #region private methods
        public void GetInitData(string id)
        {
            dal = null;
            if (DEBUG)
                dal = new MockDataLayer();
            else

                dal = new DataLayer();

            dal.Connect();
            client = dal.GetClientByID(long.Parse(id));
            ddlProducts.DisplayMember = "Name";
            ddlProducts.ValueMember = "ProductId";
            ddlProducts.DataSource = GetProductsClient();

        }
        //מביא את המוצרים שהלקוח אי פעם השתמש בהם
        private IEnumerable GetProductsClient()
        {

            return (from item in client.Samples
                    where item.PRODUCT_ID != null
                    select item.Product).Distinct().OrderBy(x => x.Name);




        }

        private List<SpecificationItemWrapper> GetSelectedSpecifications()
        {
            try
            {


                if (listBox1.SelectedItem == null || checkedListBox2.SelectedItem == null)
                //if (checkedListBox1.SelectedItem == null || checkedListBox2.SelectedItem == null)
                {
                    MessageBox.Show("לא נבחרה ספסיפיקציה ");
                    return null;
                }
                List<Grade> currentGrades = new List<Grade>();
                List<SpecificationGrade> currentSpecificationGrade = new List<SpecificationGrade>();

                foreach (var selectedItem in checkedListBox2.CheckedItems)
                {
                    Grade g = sg.Where(x => x.Name == selectedItem).FirstOrDefault();

                    currentGrades.Add(g);

                    currentSpecificationGrade.Add((from item in currentSpec.SpecificationGrade
                                                   where item.GradeId == g.GradeId &&
                                                     item.SpecificationId == currentSpec.SpecificationId
                                                   select item).FirstOrDefault());

                }




                long resultTemplateId = long.Parse(ddlResultTemplates.SelectedValue.ToString());

                List<SpecificationItem> curreSpecificationItems = (from grade in currentSpecificationGrade
                                                                   from specificationItem in grade.SPECIFICATION_ITEM
                                                                   where specificationItem.ResultTemplateId == resultTemplateId
                                                                   select specificationItem).ToList();
                if (curreSpecificationItems.Count < 1)
                {
                    MessageBox.Show("לא הוגדרה ספפיסיקציה לתוצאה הנבחרת");
                    return null;
                }

                List<SpecificationItemWrapper> gradeWrappers = new List<SpecificationItemWrapper>();


                foreach (SpecificationItem specificationItem in curreSpecificationItems)
                {
                    if (specificationItem == null)
                    {

                        MessageBox.Show("לא הוגדרה ספסיפיקציה לתוצאה המבוקשת");
                        return null;
                    }
                    if (specificationItem.SPECIFICATION_ITEM_TYPE.Name != "Numeric Range")
                    {
                        MessageBox.Show("התוכנית תומכת רק בסוג Numeric Range");
                        return null;
                    }

                    var gw = new SpecificationItemWrapper();
                    gw.GradeName = specificationItem.SPECIFICATION_GRADE.Grade.Name;
                    var parameters = specificationItem.Parameter.Split(':');
                    gw.Min = int.Parse(parameters[0]);
                    gw.Max = int.Parse(parameters[1]);
                    gradeWrappers.Add(gw);
                }


                return gradeWrappers;

                return null;
            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.Message);
                return null;
            }

        }

        #endregion

        #region Events

        private List<int> maxNumbers;
        private void GetData_Click(object sender, EventArgs e)
        {
         //   radChartView1.ShowToolTip = true;
            //סוג בדיקה שנבחר
            long resultTemplateId = long.Parse(ddlResultTemplates.SelectedValue.ToString());

            //מוצר שנבחר
            var itema = ddlProducts.SelectedItem;
            var productId = long.Parse(itema.Value.ToString());

            //טווח תאריכים
            var fromTime = dtFrom.Value;
            var toTime = dtTo.Value;




            var qq =
                from sample in client.Samples
                from aliquot in sample.Aliqouts
                from test in aliquot.Tests
                from result in test.Results
                where sample.PRODUCT_ID == productId &&
                result.COMPLETED_ON > fromTime
                 && result.COMPLETED_ON < toTime
                 && result.CalculatedNumericResult != null
                && result.ResultTemplateId == resultTemplateId
                select result;



            var grouped = (from p in qq
                           group p by new { month = p.COMPLETED_ON.Value.Month, year = p.COMPLETED_ON.Value.Year, day = p.COMPLETED_ON.Value.Day } into d
                           select new
                           {
                               dt = Convert.ToDateTime(string.Format("{0}/{1}/{2}", d.Key.day, d.Key.month, d.Key.year)),
                               count = d.Average(x => x.CalculatedNumericResult)
                           }).OrderBy(x => x.dt);

            var specifications = GetSelectedSpecifications();
            if (specifications == null)
            {
                return;
            }

            if (grouped.Count() > 0)
            {
                radChartView1.Series.Clear();
                radChartView1.LegendTitle = "CHART VIEW";
                var lineSeries = new LineSeries { Spline = false, ShowLabels = true, LegendTitle = "ממוצע תוצאות ליום", Name = MAIN_SERIES };

                var max = grouped.Max(x => x.count);


                List<int> maxNumbers = new List<int>();
                maxNumbers.Add(Convert.ToInt32(max));

                List<LineSeries> lineSeriesList = new List<LineSeries>();
                foreach (SpecificationItemWrapper itemWrapper in specifications)
                {
                    var lineSeriesmin = new LineSeries() { Name = "Min " + itemWrapper.GradeName };
                    var lineSeriesMax = new LineSeries { Name = "Max " + itemWrapper.GradeName };
                    lineSeriesmin.LegendTitle = "Min " + itemWrapper.GradeName + " " + itemWrapper.Min;
                    lineSeriesMax.LegendTitle = "Max " + itemWrapper.GradeName + " " + itemWrapper.Max;
                    foreach (var g in grouped)
                    {
                        lineSeriesmin.DataPoints.Add(new CategoricalDataPoint(itemWrapper.Min, g.dt.ToShortDateString()));
                        lineSeriesMax.DataPoints.Add(new CategoricalDataPoint(itemWrapper.Max, g.dt.ToShortDateString()));
                    }
                    lineSeriesList.Add(lineSeriesmin);
                    lineSeriesList.Add(lineSeriesMax);
                    maxNumbers.Add(itemWrapper.Max);

                }
                foreach (var g in grouped)
                {
                    var dp = new CategoricalDataPoint(Convert.ToDouble(g.count), g.dt.ToShortDateString());

                    lineSeries.DataPoints.Add(dp);
                }
                radChartView1.Series.Add(lineSeries);
                foreach (LineSeries series in lineSeriesList)
                {
                    radChartView1.Series.Add(series);

                }
                CategoricalAxis categoricalAxis = radChartView1.Axes[0] as CategoricalAxis;
                categoricalAxis.PlotMode = AxisPlotMode.OnTicksPadded;
                categoricalAxis.LabelFitMode = AxisLabelFitMode.Rotate;
                categoricalAxis.LabelRotationAngle = 310;

                LinearAxis verticalAxis = radChartView1.Axes[1] as LinearAxis;
                verticalAxis.ForeColor = Color.Green;
                verticalAxis.BorderColor = Color.DarkOrange;

                var v = maxNumbers.Max();

                SetMax(v);
            }
            else
            {
                MessageBox.Show("לא נמצאו נתונים");
            }
        }

        private void SetMax(int v)
        {
            if (v > 0)
            {
                v += 100;
            }
            numericUpDown1.Maximum = v;
            numericUpDown1.Value = v;
            trackBar1.Maximum = v;
            trackBar1.Value = v;
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                LinearAxis verticalAxis = radChartView1.Axes[1] as LinearAxis;
                if (verticalAxis != null)
                    verticalAxis.Maximum = Convert.ToDouble(numericUpDown1.Value);
            }
            catch
            { }
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            try
            {
                LinearAxis verticalAxis = radChartView1.Axes[1] as LinearAxis;
                if (verticalAxis != null)
                    verticalAxis.Maximum = trackBar1.Value;
            }
            catch
            { }
        }

        private void ddlProducts_SelectedIndexChanged(object sender, Telerik.WinControls.UI.Data.PositionChangedEventArgs e)
        {
            specifications = new List<Specification>();
            checkedListBox2.Items.Clear();
            listBox1.Items.Clear();
            listBox2.Items.Clear();
            if (ddlProducts.SelectedIndex > -1)
            {

                var itema = ddlProducts.SelectedItem;
                var productId = long.Parse(itema.Value.ToString());
                var spec = dal.GetSpecification("PRODUCT", productId);


                //Get specifications per product
                foreach (EntitySpecification entitySpecification in spec)
                {
                    specifications.Add(entitySpecification.Specification);
                    listBox1.Items.Add(entitySpecification.Specification.NAME);

                }

            }
        }

        private void checkedListBox1_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            checkedListBox2.Items.Clear();
            listBox2.Items.Clear();


            sg = new List<Grade>();
            if (listBox1.SelectedItem != null)
            //if (checkedListBox1.SelectedItem != null)
            {


                //Get GRADES PER SPECIFICATION
                //var s = checkedListBox1.SelectedItem.ToString();
                var s = listBox1.SelectedItem.ToString();
                currentSpec = specifications.Where(x => x.NAME == s).FirstOrDefault();



                foreach (var spg in currentSpec.SpecificationGrade)
                {
                    sg.Add(spg.Grade);
                    checkedListBox2.Items.Add(spg.Grade.Name);
                    listBox2.Items.Add(spg.Grade.Name);
                }


            }
        }

        private void checkedListBox2_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            var bb = checkedListBox2.SelectedItem;
            var abb = checkedListBox2.SelectedItems;
        }


        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            checkedListBox1_ItemCheck(null, null);

        }

        private void ResultsByClientCls_Resize(object sender, EventArgs e)
        {

            //עיצוב כותרת
            lblHeader.Location = new Point(Width / 2 - lblHeader.Width / 2, lblHeader.Location.Y);

        }
        #endregion

        private void radChartView1_LabelFormatting(object sender, ChartViewLabelFormattingEventArgs e)
        {




        }



    }
}