namespace EcbRatesUpdateService
{
    public class EcbRateXmlBankLt
    {
        // NOTE: Generated code may require at least .NET Framework 4.5 or .NET Core/Standard 2.0.
        /// <remarks/>
        [System.SerializableAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.lb.lt/WebServices/FxRates")]
        [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.lb.lt/WebServices/FxRates", IsNullable = false)]
        public partial class FxRates
        {

            private FxRatesFxRate[] fxRateField;

            /// <remarks/>
            [System.Xml.Serialization.XmlElementAttribute("FxRate")]
            public FxRatesFxRate[] FxRate
            {
                get
                {
                    return this.fxRateField;
                }
                set
                {
                    this.fxRateField = value;
                }
            }
        }

        /// <remarks/>
        [System.SerializableAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.lb.lt/WebServices/FxRates")]
        public partial class FxRatesFxRate
        {

            private string tpField;

            private System.DateTime dtField;

            private FxRatesFxRateCcyAmt[] ccyAmtField;

            /// <remarks/>
            public string Tp
            {
                get
                {
                    return this.tpField;
                }
                set
                {
                    this.tpField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlElementAttribute(DataType = "date")]
            public System.DateTime Dt
            {
                get
                {
                    return this.dtField;
                }
                set
                {
                    this.dtField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlElementAttribute("CcyAmt")]
            public FxRatesFxRateCcyAmt[] CcyAmt
            {
                get
                {
                    return this.ccyAmtField;
                }
                set
                {
                    this.ccyAmtField = value;
                }
            }
        }

        /// <remarks/>
        [System.SerializableAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.lb.lt/WebServices/FxRates")]
        public partial class FxRatesFxRateCcyAmt
        {

            private string ccyField;

            private decimal amtField;

            /// <remarks/>
            public string Ccy
            {
                get
                {
                    return this.ccyField;
                }
                set
                {
                    this.ccyField = value;
                }
            }

            /// <remarks/>
            public decimal Amt
            {
                get
                {
                    return this.amtField;
                }
                set
                {
                    this.amtField = value;
                }
            }
        }
    }
}
