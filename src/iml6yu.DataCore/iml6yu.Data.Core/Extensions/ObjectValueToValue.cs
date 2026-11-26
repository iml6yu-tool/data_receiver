using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.Data.Core.Extensions
{
    public static class ObjectValueToValue
    {
        /// <summary>
        /// 验证并将object转成bool类型
        /// </summary>
        /// <param name="originValue"></param>
        /// <param name="convertValue"></param>
        /// <returns></returns>
        public static bool VerifyAndConverBool(this object originValue, out bool convertValue)
        {
            convertValue = false;
            if (originValue is bool v)
            {
                convertValue = v;
                return true;
            }

            else if (originValue is int || originValue is long || originValue is uint || originValue is ulong || originValue is byte || originValue is sbyte)
            {
                if ((int)originValue == 1)
                {
                    convertValue = true;
                    return true;
                }
                if ((int)originValue == 0)
                {
                    convertValue = false;
                    return true;
                }
                return false;
            }
            else if (originValue is string s)
            {
                if (s == "1" || s == "true" || s == "True" || s == "TRUE")
                {
                    convertValue = true;
                    return true;
                }

                if (s == "0" || s == "false" || s == "False" || s == "FALSE")
                {
                    convertValue = false;
                    return true;
                }
                return false;
            }
            else
                return false;
        }

        /// <summary>
        /// 将原始值转成byte类型
        /// </summary>
        /// <param name="originValue"></param>
        /// <param name="convertValue"></param>
        /// <returns></returns>
        public static bool VerifyAndConvertByte(this object originValue, out byte convertValue)
        {
            convertValue = byte.MinValue;
            if (originValue is byte v)
            {
                convertValue = v;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 将原始值转成 short类型
        /// </summary>
        /// <param name="originValue"></param>
        /// <param name="convertValue"></param>
        /// <returns></returns>
        public static bool VerifyAndConvertShort(this object originValue, out short convertValue)
        {
            convertValue = short.MinValue;
            if (originValue is short v)
            {
                convertValue = v;
                return true;
            }
            else if (originValue is byte b)
            {
                convertValue = b;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 将原始值转成int类型
        /// </summary>
        /// <param name="originValue"></param>
        /// <param name="convertValue"></param>
        /// <returns></returns>
        public static bool VerifyAndConvertInt(this object originValue, out int convertValue)
        {
            convertValue = int.MinValue;
            if (originValue is int v)
            {
                convertValue = v;
                return true;
            }
            else if (originValue is byte b)
            {
                convertValue = b;
                return true;
            }
            else if (originValue is short s)
            {
                convertValue = s;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 将原始值转成long类型
        /// </summary>
        /// <param name="originValue"></param>
        /// <param name="convertValue"></param>
        /// <returns></returns>
        public static bool VerifyAndConvertLong(this object originValue, out long convertValue)
        {
            convertValue = long.MinValue;
            if (originValue is long v)
            {
                convertValue = v;
                return true;
            }
            else if (originValue is byte b)
            {
                convertValue = b;
                return true;
            }
            else if (originValue is short s)
            {
                convertValue = s;
                return true;
            }
            else if (originValue is int i)
            {
                convertValue = i;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 将原始值转成float类型
        /// </summary>
        /// <param name="originValue"></param>
        /// <param name="convertValue"></param>
        /// <returns></returns>
        public static bool VerifyAndConvertFloat(this object originValue, out float convertValue)
        {
            convertValue = float.MinValue;
            if (originValue is float v)
            {
                convertValue = v;
                return true;
            }
            else if (originValue is byte b)
            {
                convertValue = b;
                return true;
            }
            else if (originValue is short s)
            {
                convertValue = s;
                return true;
            }
            else if (originValue is int i)
            {
                convertValue = i;
                return true;
            }
            else if (originValue is long l)
            {
                convertValue = l;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 将原始值转成double类型
        /// </summary>
        /// <param name="originValue"></param>
        /// <param name="convertValue"></param>
        /// <returns></returns>
        public static bool VerifyAndConvertDouble(this object originValue, out double convertValue)
        {
            convertValue = double.MinValue;
            if (originValue is double v)
            {
                convertValue = v;
                return true;
            }
            else if (originValue is float f)
            {
                convertValue = f;
                return true;
            }
            else if (originValue is byte b)
            {
                convertValue = b;
                return true;
            }
            else if (originValue is short s)
            {
                convertValue = s;
                return true;
            }
            else if (originValue is int i)
            {
                convertValue = i;
                return true;
            }
            else if (originValue is long l)
            {
                convertValue = l;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 将原始值转成char类型
        /// </summary>
        /// <param name="originValue"></param>
        /// <param name="convertValue"></param>
        /// <returns></returns>
        public static bool VerifyAndConvertChar(this object originValue, out char convertValue)
        {
            convertValue = char.MinValue;
            if (originValue is char v)
            {
                convertValue = v;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 将原始值转成string类型
        /// </summary>
        /// <param name="originValue"></param>
        /// <param name="convertValue"></param>
        /// <returns></returns>
        public static bool VerifyAndConvertString(this object originValue, out string convertValue)
        {
            convertValue = string.Empty;
            if (originValue is string v)
            {
                convertValue = v;
                return true;
            }
            else if (originValue is char c)
            {
                convertValue = c.ToString();
                return true;
            }
            return false;
        }
        /// <summary>
        /// 将原始值转成Datetime类型
        /// </summary>
        /// <param name="originValue"></param>
        /// <param name="convertValue"></param>
        /// <returns></returns>
        public static bool VerifyAndConvertDatetime(this object originValue, out DateTime convertValue)
        {
            convertValue = DateTime.UnixEpoch;
            if (originValue is DateTime v)
            {
                convertValue = v;
                return true;
            }
            else if (originValue is string c)
            {
                return DateTime.TryParse(c, out convertValue); 
            }
            return false;
        } 

        /// <summary>
        /// 
        /// </summary>
        /// <param name="originValue"></param>
        /// <param name="typeCode"></param>
        /// <param name="convertValue"></param>
        /// <returns></returns>
        public static bool VerifyAndConvertValue(this object originValue, int typeCode, out object convertValue)
        {
            convertValue = null;

            if (typeCode == 3)//TypeCode.Boolean
            {
                var r = VerifyAndConverBool(originValue, out bool boolValue);
                convertValue = boolValue;
                return r;
            }

            if (typeCode == 6)// TypeCode.Byte
            {
                var r = VerifyAndConvertByte(originValue, out byte byteValue);
                convertValue = byteValue;
                return r;
            }

            if (typeCode == 7)// TypeCode.Int16
            {
                var r = VerifyAndConvertShort(originValue, out short shortValue);
                convertValue = shortValue;
                return r;
            }

            if (typeCode == 9)// TypeCode.Int32
            {
                var r = VerifyAndConvertInt(originValue, out int intValue);
                convertValue = intValue;
                return r;
            }

            if (typeCode == 11)//TypeCode.Int64
            {
                var r = VerifyAndConvertLong(originValue, out long longValue);
                convertValue = longValue;
                return r;
            }

            else if (typeCode == 4) //TypeCode.Char TypeCode.String
            {
                var r = VerifyAndConvertChar(originValue, out char charValue);
                convertValue = charValue;
                return r;
            }
            if (typeCode == 18)
            {
                var r = VerifyAndConvertString(originValue, out string stringValue);
                convertValue = stringValue;
                return r;
            }
            if (typeCode == 16)
            {
                var r = VerifyAndConvertDatetime(originValue, out DateTime dtValue);
                convertValue = dtValue;
                return r;
            }
            try
            {
                convertValue = Convert.ChangeType(originValue, (TypeCode)typeCode);
                return true;
            }
            catch
            {
                return false;
            } 
        }
    }
}
