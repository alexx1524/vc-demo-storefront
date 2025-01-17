using System.Security.Cryptography;
using System.Text;
using VirtoCommerce.Storefront.Model.Common;

namespace VirtoCommerce.LiquidThemeEngine.Filters
{
    /// <summary>
    /// String filters are used to manipulate outputs and variables of the string type.
    /// https://docs.shopify.com/themes/liquid-documentation/filters/string-filters
    /// </summary>
    public static partial class StringFilters
    {

        /// <summary>
        /// Converts a string into CamelCase.
        /// {{ 'coming-soon' | camelcase }}
        /// Result - ComingSoon
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string Camelize(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            string result = "";

            string[] strArray = input.Split('_', '-');
            foreach (string word in strArray)
            {
                result += word.Substring(0, 1).ToUpper() + word.Substring(1);
            }
            return result;
        }

        public static string Handle(string input)
        {
            return Handleize(input);
        }


        /// <summary>
        /// Formats a string into a handle.
        /// Input
        ///{{ '100% M & Ms!!!' | handleize }}
        /// Output
        /// 100-m-ms
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string Handleize(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            return input.Handelize();
        }

        /// <summary>
        /// Converts a string into an MD5 hash.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string Md5(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            byte[] hash;
            using (var md5 = MD5.Create())
            {
                hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            }

            return hash.ToHex(upperCase: false);
        }

        /// <summary>
        /// Outputs the singular or plural version of a string based on the value of a number. The first parameter is the singular string and the second parameter is the plural string.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="singular"></param>
        /// <param name="plural"></param>
        /// <returns></returns>
        public static string Pluralize(int input, string singular, string plural)
        {
            return input == 1 ? singular : plural;
        }


        /// <summary>
        /// Strips tabs, spaces, and newlines (all whitespace) from the left and right side of a string.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string Strip(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            return input.Trim();
        }

        /// <summary>
        /// Remove leading symbols from a string.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="symbols"></param>
        /// <returns></returns>
        public static string StripStart(string input, string symbols)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            return input.TrimStart(symbols.ToCharArray());
        }

        public static string Format(object input, string format)
        {
            if (input == null)
                return null;
            else if (string.IsNullOrWhiteSpace(format))
                return input.ToString();

            return string.Format("{0:" + format + "}", input);
        }

    }


}
