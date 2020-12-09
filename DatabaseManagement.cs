using System;
using Microsoft.Data.Sqlite;


namespace StockTrading
{
    public class DatabaseManagement
    {
        // undefined step variable to determine the step of the algorithm
        public string StepCounter;
        // undefined algorithm variable for all algorithms to define once.
        public string AlgorithmName;
        // equity_access_percent. This will likely be used by all algoritms.
        public decimal equity_access_percent;
        // Variables used by this class
        // state columns
        public string InputDataState = "Input_Data_State";
        public string OutputStepState = "Output_Step_State";
        //state sql pieces
        public string StateFrom = @" From State ";
        public string StateEnd = @" Where Algorithm = ";
        public string DBFunctionError = "Error from " + System.Reflection.MethodBase.GetCurrentMethod().Name + " function.";
        // roi columns (do this later)
        // SQL commander. Can only handle one sql command at a time.

        // define SQLite connection
        public SqliteConnection connection = new SqliteConnection("Data Source=trader.db;Mode=ReadWrite");
        // variables used by both algorithms and this class
        public void UpdateStateTable(string columnUpdate, string SetState, string Algorithm)
        {
            // sql pieces
            // It's not worth it to refactor yet for the algorithm part... Although I should be more careful for the ROI operations
            SetState = "\"" + SetState + "\"";
            Algorithm = "\"" + Algorithm + "\"";
            string updateStateBeginning = @"Update State SET ";

            connection.Open();
            LiteCommand(updateStateBeginning + columnUpdateParser(columnUpdate) +" = " + SetState + StateEnd + Algorithm + ";").ExecuteNonQuery();
            connection.Close();
        }
        public string ReadStateTable(string columnUpdate, string Algorithm)
        {
            Algorithm = "\"" + Algorithm + "\"";

            connection.Open();
            var dbreader = LiteCommand("Select " + columnUpdateParser(columnUpdate) + StateFrom + StateEnd + Algorithm + ";").ExecuteScalar();
            connection.Close();
            return dbreader.ToString();
        }
        public void InsertROI(string ROI, string Algorithm)
        {
            ROI = "\"" + ROI + "\"";
            Algorithm = "\"" + Algorithm + "\"";
            string date_now = "\""+DateTime.Now.ToString("M/d/yyyy")+"\"";
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO ROI (ID,Algorithm,ROI, Date_Algo_Completed) values (1,"+ Algorithm +","+ ROI +", " +date_now+");";
            connection.Open();
            command.ExecuteNonQuery();
            connection.Close();
        }
        public string columnUpdateParser(string columnUpdate)
        {
            string column = columnUpdate.ToLower();
            if (columnUpdate == "input")
            {
                return InputDataState;
            }
            else if (column == "output")
            {
                return OutputStepState;
            }
            else if (column == "initial_investment")
            {
                return "Initial_Investment";
            }
            else if (column == "equity_access_percent")
            {
                return "equity_access_percent";
            }
            else if (column == "limit_price")
            {
                return "limit_price";
            }
            else if (column == "previous_step_order_id")
            {
                return "previous_step_order_id";
            }
            else if (column == "fdq_days_after_quarter")
            {
                return "fdq_days_after_quarter";
            }
            else if (column == "sell_price")
            {
                return "sell_price";
            }
            throw new System.ArgumentException(DBFunctionError);
        }
        public SqliteCommand LiteCommand(string sqlcommand)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"" + sqlcommand;
            return command;
        }
        public string StepReader(string AlgorithmN)
        {
            return ReadStateTable("output", AlgorithmN);
        }
    }
}