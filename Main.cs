using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Transposer
{
    public partial class Main : Form
    {
        private const string SymbolPath = "Symbols.txt";
        private const string ParamsPath = "Parameters.txt";
        private const string BckClrCol = "Highlight";
        private const string SortCol = "Mid";
        private int _dataGridColCnt;

        private readonly List<BloombergSecurity> _securities = new List<BloombergSecurity>();
        //private readonly List<SymbolParms>
        private List<string> _fields = new List<string>();
        private DataTable _transposerTable = new DataTable();
        private readonly BloombergRealTimeData _bloombergRealTimeData = new BloombergRealTimeData();
        public DataGridView DataGridViewTrnspsr;

        // parameters
        private int _lookback;
        private Color _upChgColor;
        private Color _downChgColor;
        private Color _dlfColor;
        private int _highlightTimeInSecs;
        private string _baseSymbolName;
        private double _precision;
        private bool _sortGrid;

        private delegate void SetTextCallback(object sender, ListChangedEventArgs e);

        public Main()
        {
            InitializeComponent();
            InitParams();
            InitializeDataGrid();
            InitTimer();
            _bloombergRealTimeData.SendRequest();
            CorrectWindowSize();

            //ColorConverter c = new ColorConverter();
            //c.ConvertFromString("Blue");
        }

        #region Initialization

        private void InitParams()
        {
            LoadDefaultParams();
            var parameters = ReadAndParseTextFile(ParamsPath);
            if (parameters != null && parameters.Count > 0) SetParameters(parameters);
            SetFields();
        }

        private void InitializeDataGrid()
        {

            DataGridViewTrnspsr = dataGridViewTrnspsr;
            _transposerTable = FormatDataTable();
            AddSymbols(_transposerTable);

            var bindingSource1 = new BindingSource {DataSource = _transposerTable};
            dataGridViewTrnspsr.DataSource = bindingSource1;
            bindingSource1.ListChanged += bindingSource1_ListChanged;

            // error handler for datagrid threading errors
            DataGridViewTrnspsr.DataError += DataGridViewTrnspsr_DataError;

            FormatDatagrid();
        }
        
        private DataTable FormatDataTable()
        {
            var transposerTable = new DataTable();

            var symbol = transposerTable.Columns.Add("Symbol", typeof (string));
            symbol.Unique = true;

            var name = transposerTable.Columns.Add("Name", typeof (string));
            name.Unique = true;

            foreach (var field in _fields)
            {
                transposerTable.Columns.Add(field, typeof (double));
            }

            transposerTable.Columns.Add("Change", typeof (string));
            transposerTable.Columns.Add(BckClrCol, typeof (int));

            return transposerTable;
        }

        private void SetFields()
        {
            _fields = new List<string> {"Mid", "Bid", "Ask"};
        }

        private void FormatDatagrid()
        {

            dataGridViewTrnspsr.ScrollBars = ScrollBars.Horizontal;
            dataGridViewTrnspsr.RowHeadersVisible = false;
            dataGridViewTrnspsr.ColumnHeadersDefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleCenter;

            dataGridViewTrnspsr.Columns[0].ReadOnly = true;
            dataGridViewTrnspsr.Columns[0].DefaultCellStyle.BackColor = Color.LightGray;
            dataGridViewTrnspsr.Columns[1].ReadOnly = true;


            dataGridViewTrnspsr.Columns[2].ReadOnly = true;
            dataGridViewTrnspsr.Columns[2].DefaultCellStyle.Format = "#.00##";
            dataGridViewTrnspsr.Columns[3].ReadOnly = true;
            dataGridViewTrnspsr.Columns[3].DefaultCellStyle.Format = "#.00##";
            dataGridViewTrnspsr.Columns[4].ReadOnly = true;
            dataGridViewTrnspsr.Columns[4].DefaultCellStyle.Format = "#.00##";
            dataGridViewTrnspsr.Columns[5].ReadOnly = true;
            dataGridViewTrnspsr.Columns[5].DefaultCellStyle.Format = "#.0000";

            dataGridViewTrnspsr.Columns[0].Visible = false;
            dataGridViewTrnspsr.Columns[3].Visible = false;
            dataGridViewTrnspsr.Columns[4].Visible = false;
            dataGridViewTrnspsr.Columns[6].Visible = false;

            _dataGridColCnt = dataGridViewTrnspsr.Columns.Count;

            dataGridViewTrnspsr.ClearSelection();
            dataGridViewTrnspsr.CurrentCell = null;
        }

        private void AddSymbols(DataTable transposerTable)
        {
            var symbols = ReadAndParseTextFile(SymbolPath);
            var curncyDepndnt = new Dictionary<TransposedSecurity, CurncyDepncy>();

            // first symbol in the symbols.txt file is always assumed to be the base symbol
            DataRow row = transposerTable.Rows.Add();
            row[0] = symbols[0]["TICKER"];
            row[1] = symbols[0]["NAME"];

            var securityBase = new BloombergSecurity(row, _fields);
            _securities.Add(securityBase);
            _bloombergRealTimeData.AddSecurity(securityBase);
            _baseSymbolName = _transposerTable.Rows[0][1].ToString().Trim();
            securityBase.HighlightTimeInSecs = _highlightTimeInSecs;
            securityBase.Precision = _precision;

            for (int i = 1; i < symbols.Count; i++)
            {
                if (symbols[i].ContainsKey("TICKER") && symbols[i].ContainsKey("NAME"))
                {
                    DataRow tRow = transposerTable.Rows.Add();
                    tRow[0] = symbols[i]["TICKER"];
                    tRow[1] = symbols[i]["NAME"];

                    var sec = new TransposedSecurity(securityBase, tRow, _fields);
                    _securities.Add(sec);
                    _bloombergRealTimeData.AddSecurity(sec);
                    securityBase.AddTransposedSecurity(sec);
                    sec.LookBack = _lookback;
                    sec.HighlightTimeInSecs = _highlightTimeInSecs;
                    sec.Precision = _precision;

                    // if it has a currency adjustment
                    if (symbols[i].ContainsKey("CURNCY"))
                    {
                        sec.HasCurrencyDependants = true;
                        var curncyDepncy = new CurncyDepncy() {Ticker = symbols[i]["CURNCY"]};

                        if (!symbols[i].ContainsKey("CURNCYNAME"))
                            curncyDepncy.Name = symbols[i]["CURNCYNAME"];
                        else
                            curncyDepncy.Name = curncyDepncy.Ticker.Split(' ')[0];

                        if (symbols[i].ContainsKey("MULTIPLIER"))
                        {
                            double multiplier;
                            if (Double.TryParse(symbols[i]["MULTIPLIER"], out multiplier))
                                sec.CurncyMltp = multiplier;
                        }
                        curncyDepndnt.Add(sec, curncyDepncy);
                    }
                }
            }

            var curncyAdded = new Dictionary<string, TransposedSecurity>();
            foreach (var currency in curncyDepndnt)
            {
                TransposedSecurity sec;
                if (!curncyAdded.ContainsKey(currency.Value.Name))
                {
                    DataRow cRow = transposerTable.Rows.Add();
                    cRow[0] = currency.Value.Ticker;
                    cRow[1] = currency.Value.Name;
                    sec = new TransposedSecurity(securityBase, cRow, _fields);
                    _securities.Add(sec);
                    _bloombergRealTimeData.AddSecurity(sec);
                    securityBase.AddTransposedSecurity(sec);
                    sec.LookBack = _lookback;
                    sec.HighlightTimeInSecs = _highlightTimeInSecs;

                    sec.HasCurrencyDependants = true;
                    sec.IsCurrency = true;
                    sec.AddCurrencyDependantSecurity(currency.Key);

                    curncyAdded.Add(currency.Value.Name, sec);
                }
                else
                {
                    sec = curncyAdded[currency.Value.Name];
                    sec.AddCurrencyDependantSecurity(currency.Key);
                }
            }
        }

        private struct CurncyDepncy
        {
            public string Ticker;
            public string Name;
        }

        private void LoadDefaultParams()
        {
            _lookback = 100;
            _upChgColor = Color.LawnGreen;
            _downChgColor = Color.Red;
            _dlfColor = Color.White;
            _highlightTimeInSecs = 4;
            _precision = 0.0005;
            _sortGrid = false;
        }

        private static List<Dictionary<string, string>> ReadAndParseTextFile(string path)
        {
            var parsedText = new List<Dictionary<string, string>>();

            try
            {
                using (var sr = new StreamReader(path))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.Substring(0, 1) != "#")
                        {
                            string[] items = line.Split(',');
                            var kvpItem = new Dictionary<string, string>();
                            foreach (string kvItems in items)
                            {
                                string[] kvPair = kvItems.Split(';');
                                if (kvPair.Length == 2)
                                {
                                    kvpItem.Add(kvPair[0].ToUpper(), kvPair[1]);
                                    //Console.WriteLine(@"{0} {1}", kvPair[0], kvPair[1]);
                                }
                            }

                            if (kvpItem.Count > 0) parsedText.Add(kvpItem);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(@"The file could not be read:");
                Console.WriteLine(e.Message);
            }

            return parsedText;
        }

        private void SetParameters(List<Dictionary<string, string>> parameterList)
        {
            foreach (var parameters in parameterList)
            {
                foreach (var parameter in parameters)
                {
                    switch (parameter.Key)
                    {
                        case "LOOKBACK":
                            int lb;
                            if (int.TryParse(parameter.Value, out lb))
                            {
                                if (lb > 2)
                                    _lookback = lb;
                            }
                            break;
                        case "HIGHLIGHTTIMEINSECS":
                            int timeInSecs;
                            if (int.TryParse(parameter.Value, out timeInSecs))
                            {
                                if (timeInSecs > 0 && timeInSecs < 120)
                                    _highlightTimeInSecs = timeInSecs;
                            }
                            break;
                        case "PRECISION":
                            double precision;
                            if (Double.TryParse(parameter.Value, out precision))
                            {
                                if (precision > 0 && precision < 120)
                                    _precision = precision;
                            }
                            break;
                        case "SORTGRID":
                            bool sortGrid;
                            if (Boolean.TryParse(parameter.Value, out sortGrid))
                            {
                                _sortGrid = sortGrid;
                            }
                            break; //Precision
                        default:
                            Console.WriteLine(@"Bad Parameter Key/Value pair '{0}' '{1}'", parameter.Key,
                                              parameter.Value);
                            break;

                    }
                }
            }
        }

        private void InitTimer()
        {
            //Instantiate the timer
            foreach (var bloombergSecurity in _securities)
            {
                timer1 = new Timer();
                timer1.Interval = 100; //1000ms = 1sec
                bloombergSecurity.IntiTimer(timer1);
                timer1.Start();
            }
        }

        public void CorrectWindowSize()
        {
            // Resize the master DataGridView columns to fit the newly loaded data.
            dataGridViewTrnspsr.AutoResizeColumns();

            // Configure the details DataGridView so that its columns automatically 
            // adjust their widths when the data changes.
            //dataGridViewTrnspsr.AutoSizeColumnsMode =
            //     DataGridViewAutoSizeColumnsMode.AllCells;

            int width = WinObjFunctions.CountGridWidth(dataGridViewTrnspsr);
            int height = WinObjFunctions.CountGridHeight(dataGridViewTrnspsr);
            ClientSize = new Size(width, height);
        }

        public static class WinObjFunctions
        {
            public static int CountGridWidth(DataGridView dgv)
            {
                int width = 0;
                for (int index = 0; index < dgv.Columns.Count; index++)
                {
                    DataGridViewColumn column = dgv.Columns[index];
                    if (column.Visible)
                        width += column.Width;
                }
                return width + 2;
            }

            public static int CountGridHeight(DataGridView dgv)
            {
                int width = 0;
                for (int index = 0; index < dgv.Rows.Count; index++)
                {
                    DataGridViewRow rows = dgv.Rows[index];
                    if (rows.Visible)
                        width += rows.Height;
                }
                return width + 44;
            }
        }

        #endregion

        public void bindingSource1_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                var d = new SetTextCallback(bindingSource1_ListChanged);
                Invoke(d, new[] { sender, e });
            }
            else
            {

                // sort by mid price
                if (_sortGrid)
                    if (DataGridViewTrnspsr.Columns.Contains(SortCol))
                        if (e.PropertyDescriptor != null && String.Equals(e.PropertyDescriptor.Name, SortCol))
                            DataGridViewTrnspsr.Sort(DataGridViewTrnspsr.Columns[SortCol], ListSortDirection.Descending);

                if (dataGridViewTrnspsr != null)
                {
                    // Style for base scurity 
                    foreach (DataGridViewRow row in dataGridViewTrnspsr.Rows)
                    {
                        var secName = row.Cells[1].Value.ToString().Trim();

                        int direction;
                        int.TryParse(row.Cells[BckClrCol].Value.ToString(), out direction);

                        //Console.WriteLine(@"{0} {1} dir {2}", baseSymbolName, secName, direction);

                        row.DefaultCellStyle.Font =
                            String.Equals(secName, _baseSymbolName)
                                ? new Font(Font, FontStyle.Bold)
                                : new Font(Font, FontStyle.Regular);

                        if (int.TryParse(row.Cells[BckClrCol].Value.ToString(), out direction))
                        {
                            var cellStyle = new DataGridViewCellStyle();
                            if (direction < 0)
                                cellStyle.BackColor = _downChgColor;
                            else
                                cellStyle.BackColor = direction > 0 ? _upChgColor : _dlfColor;

                            for (int i = 0; i < _dataGridColCnt; i++)
                                row.Cells[i].Style = cellStyle;
                        }
                    }
                }

                if (dataGridViewTrnspsr.CurrentCell != null)
                    dataGridViewTrnspsr.CurrentCell = null;
            }
        }

        private void DataGridViewTrnspsr_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            Console.WriteLine(@"DataGridError: {0} {1} rowIdx {2}", e.Context, e.Exception.Message, e.RowIndex);
        }


    }
}
