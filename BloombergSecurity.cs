using System;
using System.Collections.Generic;
using System.Drawing;
using System.Data;
using System.Windows.Forms;

namespace Transposer
{
    public class BloombergSecurity
    {
        public DataRow SecurityData { get; set; }
        public DataGridViewRow DataGridRow { get; set; }
        public List<string> SecurityFields = new List<string>();
        public string Name { get; private set; }
        public string Ticker { get; private set; }

        private DateTime _lastBackColorChg = DateTime.Now;
        private bool _backColorNeedsCheck = false;
        private const double HighlightTimeInsecs = 4;

        private double _ask;
        private bool _prevAskInit;
        private double _bid;
        private bool _prevBidInit;
        private double _mid;
        private bool _prevMidInit;
        private double _chg;

        public double Ask
        {
            get { return _ask; }
            private set
            {
                if (!_prevAskInit)
                    _prevAskInit = true;

                _ask = Math.Round(value, 6);

                if ((_prevBidInit) && (_prevAskInit))
                    Mid = (_bid + _ask)/2;
            }
        }

        public double Bid
        {
            get { return _bid; }
            private set
            {
                if (!_prevBidInit)
                    _prevBidInit = true;

                _bid = Math.Round(value, 6); ;

                if ((_prevBidInit) && (_prevAskInit))
                    Mid = (_bid + _ask)/2;
            }
        }

        public double Mid
        {
            get { return _mid; }
            private set
            {
                if (_prevMidInit)
                {
                    // if the new value for mid is different from the prev mid
                    if (Math.Abs(PrevMid - value) > 1E-14)
                    {
                        PrevMid = _mid;
                        _mid = value;
                        UpdateMid();
                        Change = _mid - PrevMid;

                        //Console.WriteLine("{0} Mid {1} Prev {2} Chg {3}",Name,_mid, PrevMid,Change);
                    }
                }
                else
                {
                    _mid = value;
                    PrevMid = _mid;
                    _prevMidInit = true;
                }
            }
        }

        public double PrevMid { get; private set; }

        public double Change
        {
            get { return _chg; }
            private set {
                if (value != 0)
                {
                    _chg = Math.Round(value, 5); 
                    UpdateChange();
                }
            }
        }
        
        public BloombergSecurity(DataGridViewRow dataGridrow, DataRow dataRow, List<string> securityField, Form parent)
        {
            DataGridRow = dataGridrow;
            SecurityData = dataRow;
            Name = dataRow[1].ToString();
            Ticker = dataRow[0].ToString();
            SecurityFields = securityField;
        }

        public void IntiTimer(Timer timer1)
        {
            //Instantiate the timer
            timer1.Tick += new EventHandler(timer1_Tick);
        }

        public void IntiTimer2(Timer timer2)
        {
            // Instantiate this timer for testing only
            timer2.Tick += new EventHandler(timer2_Tick);
        }

        public void Setfield(String field, string value)
        {
            SecurityData[field] = value;
            if (field == "Bid") Bid = Double.Parse(value);
            if (field == "Ask") Ask = Double.Parse(value);
        }

        public void SetDelayedStream()
        {
            foreach (DataGridViewCell cell in DataGridRow.Cells)
                cell.Style.BackColor = Color.Yellow;
        }

        private void UpdateMid()
        {
            SecurityData["Mid"] = Mid;
        }

        private void UpdateChange()
        {
            SecurityData["Change"] = Change;
            if (Change < 0)
            {
                HighlightBackColor(false);
            }
            else
            {
                if (Change > 0)
                {
                    HighlightBackColor(true);
                }
            }
        }

        private void HighlightBackColor(bool isUp)
        {
            if (isUp)
            {
                SecurityData["Highlight"] = 1;
            }
            else
            {
                SecurityData["Highlight"] = -1;
            }
            _lastBackColorChg = DateTime.Now;
            _backColorNeedsCheck = true;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (_backColorNeedsCheck)
            {
                var timeSinceChange = DateTime.Now - _lastBackColorChg;
                if (timeSinceChange.Seconds >= HighlightTimeInsecs)
                {
                    _backColorNeedsCheck = false;
                    SecurityData["Highlight"] = 0;
                }
            }
        }
        
        private void timer2_Tick(object sender, EventArgs e)
        {
            var rnd = new Random();
            int bias = rnd.Next(-10, 10);
            Change = bias;
        }
    }
}
