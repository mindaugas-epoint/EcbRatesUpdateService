using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Xml.Serialization;
using Database_Access_Layer;

namespace EcbRatesUpdateService
{
    public class EcbRate
    {
        public string? CurrCode { get; set; }
        public decimal? Rate { get; set; }
        public DateTime RateDate { get; set; }
        public decimal? RateChange { get; set; }
    }

    public interface IEcbRatesUpdate
    {
        Task<List<EcbRate>> GetEcbRates(DateOnly date);
        Task UpdateEcbRatesOneByOne(IDbContext dbContext);
        Task UpdateEcbRates(IDbContext dbContext);
    }

    public class EcbRatesUpdate : IEcbRatesUpdate
    {
        static readonly HttpClient client = new HttpClient();
        public async Task<List<EcbRate>> GetEcbRates(DateOnly date)
        {
            DateTimeFormatInfo fmt = (new CultureInfo("lt-LT")).DateTimeFormat;
            string QueryParams = string.Format("tp=EU&dt={0}", date.ToString("d", fmt));
            string xmlURL = string.Format("http://www.lb.lt/webservices/FxRates/FxRates.asmx/getFxRates?{0}", QueryParams);

            HttpResponseMessage response = await client.GetAsync(xmlURL);
            response.EnsureSuccessStatusCode();
            Stream receiveStream = await response.Content.ReadAsStreamAsync();

            var xmlRoot = new XmlRootAttribute();
            xmlRoot.ElementName = "FxRates";
            xmlRoot.Namespace = "http://www.lb.lt/WebServices/FxRates";
            XmlSerializer serializer = new XmlSerializer(typeof(EcbRateXmlBankLt.FxRates), xmlRoot);
            EcbRateXmlBankLt.FxRates rates = (EcbRateXmlBankLt.FxRates)serializer.Deserialize(receiveStream);

            receiveStream.Close();

            var ecbRates = new List<EcbRate>();
            for (var i = 0; i < rates.FxRate.Length; i++)
            {
                var ecbRate = new EcbRate();
                ecbRate.RateDate = rates.FxRate[i].Dt;
                ecbRate.CurrCode = rates.FxRate[i].CcyAmt[1].Ccy;
                ecbRate.Rate = rates.FxRate[i].CcyAmt[1].Amt;
                ecbRate.RateChange = 0;
                ecbRates.Add(ecbRate);
            }

            return ecbRates;
        }


        /// <summary>
        /// Updates ECB rates one by one 
        /// should be used with MySql stored procedure
        /// </summary>
        /// <param name="dbContext"></param>
        /// <exception cref="Exception"></exception>
        public async Task UpdateEcbRatesOneByOne(IDbContext dbContext)
        {
            try
            {
                DayOfWeek day = DateTime.Now.DayOfWeek;
                if (day != DayOfWeek.Saturday && day != DayOfWeek.Sunday)
                {
                    List<EcbRate> ecbRates = await GetEcbRates(DateOnly.FromDateTime(DateTime.Now));

                    foreach (EcbRate ecbRate in ecbRates)
                    {
                        var parameters = new List<DalDbParameter>();
                        parameters.Add(new DalDbParameter("@CurrCode", ecbRate.CurrCode, null));
                        parameters.Add(new DalDbParameter("@Rate", ecbRate.Rate, null));
                        await dbContext.ExecuteProcedureAsync("spUpdateEcbRates", parameters);
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"ECB rates update failed: {ex.Message}";
                throw new Exception(errorMessage);
            }
        }


        /// <summary>
        /// Updates ECB rates
        /// </summary>
        /// <param name="dbContext"></param>
        /// <exception cref="Exception"></exception>
        public async Task UpdateEcbRates(IDbContext dbContext)
        {
            try
            {
                DayOfWeek day = DateTime.Now.DayOfWeek;
                if (day != DayOfWeek.Saturday && day != DayOfWeek.Sunday)
                {
                    List<EcbRate> ecbRates = await GetEcbRates(DateOnly.FromDateTime(DateTime.Now));

                    DataTable currencyRatesDt = ConvertToDataTable(ecbRates, new List<string> { "RateDate", "RateChange" });

                    var parameters = new List<DalDbParameter>();
                    parameters.Add(new DalDbParameter("EcbRates", currencyRatesDt, SqlDbType.Structured));
                    await dbContext.ExecuteProcedureAsync("spUpdateEcbRates", parameters);
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"ECB rates update failed: {ex.Message}";
                throw new Exception(errorMessage);
            }
        }

        public DataTable ConvertToDataTable<T>(IList<T> data, List<string> columnsToIgnore)
        {
            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(typeof(T));
            DataTable table = new DataTable();
            foreach (PropertyDescriptor prop in properties)
            {
                if (!columnsToIgnore.Contains(prop.Name))
                {
                    table.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                }

            }

            foreach (T item in data)
            {
                DataRow row = table.NewRow();
                foreach (PropertyDescriptor prop in properties)
                {
                    if (!columnsToIgnore.Contains(prop.Name))
                    {
                        row[prop.Name] = prop.GetValue(item) ?? DBNull.Value;
                    }
                }

                table.Rows.Add(row);
            }
            return table;

        }
    }
}

