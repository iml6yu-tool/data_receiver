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
        /// 将一个object类型的Value进行类型校验并且转成对应的类型
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="originValue"></param> 
        /// <param name="targetValue"></param>
        /// <returns></returns>
        public static bool VerifyAndConvert<T>(this object originValue, out T targetValue)
            where T : struct
        {
            targetValue = default(T);
            if (originValue is T v)
            {
                targetValue = v;
                return true;
            }

            var typeCode = Convert.GetTypeCode(targetValue);

            if (typeCode == TypeCode.Boolean)
            {
                if (originValue is bool)
                {
                    targetValue = (T)originValue;
                    return true;
                }

                else if (originValue is int || originValue is long || originValue is uint || originValue is ulong || originValue is byte || originValue is sbyte)
                {
                    if ((int)originValue == 1)
                    {
                        targetValue = (T)(object)true;
                        return true;
                    }
                    if ((int)originValue == 0)
                    {
                        targetValue = (T)(object)false;
                        return true;
                    }
                    return false;
                }
                else if (originValue is string s)
                {
                    if (s == "1" || s == "true" || s == "True" || s == "TRUE")
                    {
                        targetValue = (T)(object)true;
                        return true;
                    }

                    if (s == "0" || s == "false" || s == "False" || s == "FALSE")
                    {
                        targetValue = (T)(object)false;
                        return true;
                    }
                    return false;
                }
                else
                    return false;
            }

            if (typeCode == TypeCode.Byte)
            {
                if (originValue is byte)
                {
                    targetValue = (T)originValue;
                    return true;
                }
                return false;
            }

            if (typeCode == TypeCode.Int16)
            {
                if (originValue is short)
                {
                    targetValue = (T)originValue;
                    return true;
                }
                return false;
            }

            if (typeCode == TypeCode.Int32)
            {
                if (originValue is int)
                {
                    targetValue = (T)originValue;
                    return true;
                }
                return false;
            }

            if (typeCode == TypeCode.Int64)
            {
                if (originValue is long)
                {
                    targetValue = (T)originValue;
                    return true;
                }
                return false;
            }

            if (typeCode == TypeCode.Char)
            {
                if (originValue is Char)
                {
                    targetValue = (T)originValue;
                    return true;
                }
                return false;
            }

            if (typeCode == TypeCode.String)
            {
                if (originValue is String)
                {
                    targetValue = (T)originValue;
                    return true;
                }
                return false;
            }


            try
            {
                targetValue = (T)Convert.ChangeType(originValue, typeCode);
                return true;
            }
            catch
            {
                return false;
            }

        }


        /// <summary>
        /// 将一个object类型的Value进行类型校验并且转成对应的类型
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="originValue"></param>
        /// <param name="typeCode"></param>
        /// <param name="targetValue"></param>
        /// <returns></returns>
        public static bool VerifyAndConvertValue<T>(this object originValue, int typeCode, out T targetValue)
        {
            targetValue = default(T);
            if (typeCode == 3)//TypeCode.Boolean
            {
                if (originValue is bool)
                {
                    targetValue = (T)originValue;
                    return true;
                }

                else if (originValue is int || originValue is long || originValue is uint || originValue is ulong || originValue is byte || originValue is sbyte)
                {
                    if ((int)originValue == 1)
                    {
                        targetValue = (T)(object)true;
                        return true;
                    }
                    if ((int)originValue == 0)
                    {
                        targetValue = (T)(object)false;
                        return true;
                    }
                    return false;
                }
                else if (originValue is string s)
                {
                    if (s == "1" || s == "true" || s == "True" || s == "TRUE")
                    {
                        targetValue = (T)(object)true;
                        return true;
                    }

                    if (s == "0" || s == "false" || s == "False" || s == "FALSE")
                    {
                        targetValue = (T)(object)false;
                        return true;
                    }
                    return false;
                }
                else return false;
            }
            else if (typeCode == 6 || typeCode == 7 || typeCode == 9 || typeCode == 11) //TypeCode.Byte TypeCode.Int16 TypeCode.Int32 TypeCode.Int64
            {
                if (originValue is byte b)
                {
                    targetValue = (T)originValue;
                    return true;
                }
                if (originValue is short s)
                {
                    targetValue = (T)originValue;
                    return true;
                }
                if (originValue is int)
                {
                    targetValue = (T)originValue;
                    return true;
                }
                if (originValue is long)
                {
                    targetValue = (T)originValue;
                    return true;
                }
                return false;
            }
            else if (typeCode == 10 || typeCode == 12) //TypeCode.UInt32 TypeCode.UInt64
            {
                if (originValue is uint)
                {
                    targetValue = (T)originValue;
                    return true;
                }
                if (originValue is ulong)
                {
                    targetValue = (T)originValue;
                    return true;
                }
                return false;
            }
            else if (typeCode == 4 || typeCode == 16) //TypeCode.Char TypeCode.String
            {
                if (originValue is char)
                {
                    targetValue = (T)originValue;
                    return true;
                }
                if (originValue is string)
                {
                    targetValue = (T)originValue;
                    return true;
                }
                return false;
            }
            else
            {
                try
                {
                    targetValue = (T)Convert.ChangeType(originValue, (TypeCode)typeCode);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="originValue"></param>
        /// <param name="typeCode"></param>
        /// <param name="targetValue"></param>
        /// <returns></returns>
        public static bool VerifyAndConvertValue(this object originValue, int typeCode, out object targetValue)
        {
            targetValue = null;
            if (typeCode == 3)//TypeCode.Boolean
            {
                if (originValue is bool v)
                {
                    targetValue = v;
                    return true;
                }

                else if (originValue is int || originValue is long || originValue is uint || originValue is ulong || originValue is byte || originValue is sbyte)
                {
                    if ((int)originValue == 1)
                    {
                        targetValue = true;
                        return true;
                    }
                    if ((int)originValue == 0)
                    {
                        targetValue = false;
                        return true;
                    }
                    return false;
                }
                else if (originValue is string s)
                {
                    if (s == "1" || s == "true" || s == "True" || s == "TRUE")
                    {
                        targetValue = true;
                        return true;
                    }

                    if (s == "0" || s == "false" || s == "False" || s == "FALSE")
                    {
                        targetValue = false;
                        return true;
                    }
                    return false;
                }
                else return false;
            }
            else if (typeCode == 6 || typeCode == 7 || typeCode == 9 || typeCode == 11) //TypeCode.Byte TypeCode.Int16 TypeCode.Int32 TypeCode.Int64
            {
                if (originValue is byte b)
                {
                    targetValue = b;
                    return true;
                }
                if (originValue is short s)
                {
                    targetValue = s;
                    return true;
                }
                if (originValue is int i)
                {
                    targetValue = i;
                    return true;
                }
                if (originValue is long l)
                {
                    targetValue = l;
                    return true;
                }
                return false;
            }
            else if (typeCode == 10 || typeCode == 12) //TypeCode.UInt32 TypeCode.UInt64
            {
                if (originValue is uint ui)
                {
                    targetValue = ui;
                    return true;
                }
                if (originValue is ulong ul)
                {
                    targetValue = ul;
                    return true;
                }
                return false;
            }
            else if (typeCode == 4 || typeCode == 16) //TypeCode.Char TypeCode.String
            {
                if (originValue is char c)
                {
                    targetValue = c;
                    return true;
                }
                if (originValue is string s)
                {
                    targetValue = s;
                    return true;
                }
                return false;
            }
            else
            {
                try
                {
                    targetValue = Convert.ChangeType(originValue, (TypeCode)typeCode);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
