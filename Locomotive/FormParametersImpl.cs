using ICReport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace util
{
    partial class FormParameters
    {

        public void ExecuteImpl(Report report)
        {

            


        }
        public void Execute(Report report)
        {
            try
            {
                ExecuteImpl(report);
                String retout = TransformText();
                Console.Write(retout);
            }
            catch (Exception ex)
            {
                Console.Write("ERROR: " + ex.Message);
            }            
            report.shutdown();
        }
    }
}
