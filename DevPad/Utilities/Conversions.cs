using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
#if !NO_JSON
using System.Text.Json;
#endif

namespace DevPad.Utilities
{
    public static class Conversions
    {
        private static readonly char[] _enumSeparators = new char[] { ',', ';', '+', '|', ' ' };

        public static bool IsFlagsEnum(this Type enumType)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            if (!enumType.IsEnum)
                throw new ArgumentException(null, nameof(enumType));

            return enumType.IsDefined(typeof(FlagsAttribute), true);
        }

        public static T CoerceToEnum<T>(object input) => (T)CoerceToEnum(typeof(T), input);
        public static object CoerceToEnum(this Type enumType, object input)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            if (!enumType.IsEnum)
                throw new ArgumentException(null, nameof(enumType));

            if (!TryChangeType(input, enumType, out object result))
                return Activator.CreateInstance(enumType);

            var s = result.ToString();
            if (!char.IsDigit(s[0]) && s[0] != '-')
                return result;

            if (!IsFlagsEnum(enumType))
                return Activator.CreateInstance(enumType);

            var names = Enum.GetNames(enumType);
            if (names.Length == 0)
                return Activator.CreateInstance(enumType);

            var values = Enum.GetValues(enumType);
            var tc = Type.GetTypeCode(result.GetType());
            if (tc == TypeCode.Int32 || tc == TypeCode.Int16 || tc == TypeCode.Int64 || tc == TypeCode.SByte)
            {
                long lvalue = 0;
                var l = long.Parse(s);
                for (int i = 0; i < names.Length; i++)
                {
                    if (TryChangeType(values.GetValue(i), out long vl) && vl != 0 && (l & vl) == vl)
                    {
                        lvalue |= vl;
                    }
                }
                return ChangeType(lvalue, enumType);
            }

            ulong ulvalue = 0;
            var ul = ulong.Parse(s);
            for (int i = 0; i < names.Length; i++)
            {
                if (TryChangeType(values.GetValue(i), out ulong vul) && vul != 0 && (ul & vul) == vul)
                {
                    ulvalue |= vul;
                }
            }
            return ChangeType(ulvalue, enumType);
        }

        public static bool TryCoerceToEnum<T>(object input, out object value) => TryCoerceToEnum(typeof(T), input, out value);
        public static bool TryCoerceToEnum(Type enumType, object input, out object value)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            if (!enumType.IsEnum)
                throw new ArgumentException(null, nameof(enumType));

            if (!TryChangeType(input, enumType, out object result))
            {
                value = Activator.CreateInstance(enumType);
                return false;
            }

            var s = result.ToString();
            if (!char.IsDigit(s[0]) && s[0] != '-')
            {
                value = result;
                return true;
            }

            value = Activator.CreateInstance(enumType);
            return false;
        }

        public static Type GetElementType(Type collectionType)
        {
            if (collectionType == null)
                throw new ArgumentNullException(nameof(collectionType));

            foreach (var iface in collectionType.GetInterfaces())
            {
                if (!iface.IsGenericType)
                    continue;

                if (iface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                    return iface.GetGenericArguments()[1];

                if (iface.GetGenericTypeDefinition() == typeof(IList<>))
                    return iface.GetGenericArguments()[0];

                if (iface.GetGenericTypeDefinition() == typeof(ICollection<>))
                    return iface.GetGenericArguments()[0];

                if (iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return iface.GetGenericArguments()[0];
            }
            return typeof(object);
        }

        public static string Decamelize(string text) => Decamelize(text, DecamelizeOptions.Default);
        public static string Decamelize(string text, DecamelizeOptions options)
        {
            // input: a string like loadByWhateverStuff
            // output: a string like Load By Whatever Stuff
            // BBKing -> BBKing
            // BBOKing -> BboKing
            // LoadBy25Years -> Load By 25 Years
            // SoftFluent.PetShop -> Soft Fluent. Pet Shop
            // Data2_FileName -> Data 2 File Name
            // _WhatIs -> _What is
            // __WhatIs -> __What is
            // __What__Is -> __What is
            // MyParam1 -> My Param 1
            // MyParam1Baby -> My Param1 Baby (if DontDecamelizeNumbers)

            if (string.IsNullOrWhiteSpace(text))
                return text;

            var lastCategory = CharUnicodeInfo.GetUnicodeCategory(text[0]);
            var prevCategory = lastCategory;
            if (lastCategory == UnicodeCategory.UppercaseLetter)
            {
                lastCategory = UnicodeCategory.LowercaseLetter;
            }

            int i = 0;
            bool firstIsStillUnderscore = text[0] == '_';
            var sb = new StringBuilder(text.Length);

            bool separated = false;
            var cat = char.GetUnicodeCategory(text[0]);
            switch (cat)
            {
                case UnicodeCategory.ClosePunctuation:
                case UnicodeCategory.ConnectorPunctuation:
                case UnicodeCategory.DashPunctuation:
                case UnicodeCategory.EnclosingMark:
                case UnicodeCategory.FinalQuotePunctuation:
                case UnicodeCategory.Format:
                case UnicodeCategory.InitialQuotePunctuation:
                case UnicodeCategory.LineSeparator:
                case UnicodeCategory.OpenPunctuation:
                case UnicodeCategory.OtherPunctuation:
                case UnicodeCategory.ParagraphSeparator:
                case UnicodeCategory.SpaceSeparator:
                case UnicodeCategory.SpacingCombiningMark:
                    separated = true;
                    break;

            }

            if (options.HasFlag(DecamelizeOptions.UnescapeUnicode) && CanUnicodeEscape(text, 0))
            {
                sb.Append(GetUnicodeEscape(text, ref i));
            }
            else if (options.HasFlag(DecamelizeOptions.UnescapeHexadecimal) && CanHexadecimalEscape(text, 0))
            {
                sb.Append(GetHexadecimalEscape(text, ref i));
            }
            else
            {
                if (options.HasFlag(DecamelizeOptions.ForceFirstUpper))
                {
                    sb.Append(char.ToUpper(text[0]));
                }
                else
                {
                    sb.Append(text[0]);
                }
            }

            for (i++; i < text.Length; i++)
            {
                char c = text[i];
                if (options.HasFlag(DecamelizeOptions.UnescapeUnicode) && CanUnicodeEscape(text, i))
                {
                    sb.Append(GetUnicodeEscape(text, ref i));
                    separated = true;
                }
                else if (options.HasFlag(DecamelizeOptions.UnescapeHexadecimal) && CanHexadecimalEscape(text, i))
                {
                    sb.Append(GetHexadecimalEscape(text, ref i));
                    separated = true;
                }
                else if (c == '_')
                {
                    if (!firstIsStillUnderscore || !options.HasFlag(DecamelizeOptions.KeepFirstUnderscores))
                    {
                        sb.Append(' ');
                        separated = true;
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    var category = CharUnicodeInfo.GetUnicodeCategory(c);
                    switch (category)
                    {
                        case UnicodeCategory.ClosePunctuation:
                        case UnicodeCategory.ConnectorPunctuation:
                        case UnicodeCategory.DashPunctuation:
                        case UnicodeCategory.EnclosingMark:
                        case UnicodeCategory.FinalQuotePunctuation:
                        case UnicodeCategory.Format:
                        case UnicodeCategory.InitialQuotePunctuation:
                        case UnicodeCategory.LineSeparator:
                        case UnicodeCategory.OpenPunctuation:
                        case UnicodeCategory.OtherPunctuation:
                        case UnicodeCategory.ParagraphSeparator:
                        case UnicodeCategory.SpaceSeparator:
                        case UnicodeCategory.SpacingCombiningMark:
                            if (options.HasFlag(DecamelizeOptions.KeepFormattingIndices) && c == '{')
                            {
                                while (c != '}')
                                {
                                    c = text[i++];
                                    sb.Append(c);
                                }

                                i--;
                                separated = true;
                                break;
                            }

                            if (options.HasFlag(DecamelizeOptions.ForceRestLower))
                            {
                                sb.Append(char.ToLower(c));
                            }
                            else
                            {
                                sb.Append(c);
                            }

                            if (c != ' ' && !separated)
                            {
                                sb.Append(' ');
                            }
                            separated = true;
                            break;

                        case UnicodeCategory.LetterNumber:
                        case UnicodeCategory.DecimalDigitNumber:
                        case UnicodeCategory.OtherNumber:

                        case UnicodeCategory.CurrencySymbol:
                        case UnicodeCategory.LowercaseLetter:
                        case UnicodeCategory.MathSymbol:
                        case UnicodeCategory.ModifierLetter:
                        case UnicodeCategory.ModifierSymbol:
                        case UnicodeCategory.NonSpacingMark:
                        case UnicodeCategory.OtherLetter:
                        case UnicodeCategory.OtherNotAssigned:
                        case UnicodeCategory.Control:
                        case UnicodeCategory.OtherSymbol:
                        case UnicodeCategory.Surrogate:
                        case UnicodeCategory.PrivateUse:
                        case UnicodeCategory.TitlecaseLetter:
                        case UnicodeCategory.UppercaseLetter:
                            if (category != lastCategory && c != ' ' && IsNewCategory(category, options))
                            {
                                if (!separated && prevCategory != UnicodeCategory.UppercaseLetter &&
                                    (!firstIsStillUnderscore || !options.HasFlag(DecamelizeOptions.KeepFirstUnderscores)))
                                {
                                    sb.Append(' ');
                                }

                                if (options.HasFlag(DecamelizeOptions.ForceRestLower))
                                {
                                    sb.Append(char.ToLower(c));
                                }
                                else
                                {
                                    sb.Append(char.ToUpper(c));
                                }

                                var upper = char.ToUpper(c);
                                category = CharUnicodeInfo.GetUnicodeCategory(upper);
                                if (category == UnicodeCategory.UppercaseLetter)
                                {
                                    lastCategory = UnicodeCategory.LowercaseLetter;
                                }
                                else
                                {
                                    lastCategory = category;
                                }
                            }
                            else
                            {
                                if (options.HasFlag(DecamelizeOptions.ForceRestLower))
                                {
                                    sb.Append(char.ToLower(c));
                                }
                                else
                                {
                                    sb.Append(c);
                                }
                            }
                            separated = false;
                            break;
                    }

                    firstIsStillUnderscore = firstIsStillUnderscore && c == '_';
                    prevCategory = category;
                }
            }

            if (options.HasFlag(DecamelizeOptions.ReplaceSpacesByUnderscore))
                return sb.Replace(' ', '_').ToString();

            if (options.HasFlag(DecamelizeOptions.ReplaceSpacesByMinus))
                return sb.Replace(' ', '-').ToString();

            if (options.HasFlag(DecamelizeOptions.ReplaceSpacesByDot))
                return sb.Replace(' ', '.').ToString();

            return sb.ToString();
        }

        // note: we don't want to use char.IsDigit nor char.IsNumber
        private static bool IsAsciiNumber(char c) => c >= '0' && c <= '9';
        private static bool CanUnicodeEscape(string text, int i) => (i + 5) < text.Length && text[i] == '\\' && text[i + 1] == 'u' && IsAsciiNumber(text[i + 2]) && IsAsciiNumber(text[i + 3]) && IsAsciiNumber(text[i + 4]) && IsAsciiNumber(text[i + 5]);
        private static bool IsHexNumber(char c) => IsAsciiNumber(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        private static bool CanHexadecimalEscape(string text, int i) => (i + 6) < text.Length && text[i] == '_' && text[i + 1] == 'x' && text[i + 6] == '_' && IsHexNumber(text[i + 2]) && IsHexNumber(text[i + 3]) && IsHexNumber(text[i + 4]) && IsHexNumber(text[i + 5]);

        private static char GetUnicodeEscape(string text, ref int i)
        {
            i += 5;
            return (char)int.Parse(text.Substring(2, 4));
        }


        private static char GetHexadecimalEscape(string text, ref int i)
        {
            i += 6;
            return (char)int.Parse(text.Substring(2, 4), NumberStyles.HexNumber);
        }

        private static bool IsNewCategory(UnicodeCategory category, DecamelizeOptions options)
        {
            if (options.HasFlag(DecamelizeOptions.DontDecamelizeNumbers))
            {
                if (category == UnicodeCategory.LetterNumber ||
                    category == UnicodeCategory.DecimalDigitNumber ||
                    category == UnicodeCategory.OtherNumber)
                    return false;
            }
            return true;
        }

        public static string LowerFirst(this string text)
        {
            if (text == null)
                return null;

            if (text.Length == 0)
                return text;

            if (char.IsLower(text[0]))
                return text;

            return char.ToLowerInvariant(text[0]) + text.Substring(1);
        }

        public static string UpperFirst(this string text)
        {
            if (text == null)
                return null;

            if (text.Length == 0)
                return text;

            if (char.IsUpper(text[0]))
                return text;

            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        public static Type GetEnumeratedType(Type collectionType)
        {
            if (collectionType == null)
                throw new ArgumentNullException(nameof(collectionType));

            foreach (Type type in collectionType.GetInterfaces())
            {
                if (!type.IsGenericType)
                    continue;

                if (type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return type.GetGenericArguments()[0];

                if (type.GetGenericTypeDefinition() == typeof(ICollection<>))
                    return type.GetGenericArguments()[0];

                if (type.GetGenericTypeDefinition() == typeof(IList<>))
                    return type.GetGenericArguments()[0];
            }
            return null;
        }

        public static long ToPositiveFileTime(DateTime dt)
        {
            var ft = ToFileTimeUtc(dt.ToUniversalTime());
            return ft < 0 ? 0 : ft;
        }

        public static long ToPositiveFileTimeUtc(DateTime dt)
        {
            var ft = ToFileTimeUtc(dt);
            return ft < 0 ? 0 : ft;
        }

        public static long ToFileTime(DateTime dt) => ToFileTimeUtc(dt.ToUniversalTime());
        public static long ToFileTimeUtc(DateTime dt)
        {
            const long ticksPerMillisecond = 10000;
            const long ticksPerSecond = ticksPerMillisecond * 1000;
            const long ticksPerMinute = ticksPerSecond * 60;
            const long ticksPerHour = ticksPerMinute * 60;
            const long ticksPerDay = ticksPerHour * 24;
            const int daysPerYear = 365;
            const int daysPer4Years = daysPerYear * 4 + 1;
            const int daysPer100Years = daysPer4Years * 25 - 1;
            const int daysPer400Years = daysPer100Years * 4 + 1;
            const int daysTo1601 = daysPer400Years * 4;
            const long fileTimeOffset = daysTo1601 * ticksPerDay;
            long ticks = dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime().Ticks : dt.Ticks;
            ticks -= fileTimeOffset;
            return ticks;
        }

        public static Guid ComputeGuidHash(string text)
        {
            if (text == null)
                return Guid.Empty;

            using (var md5 = MD5.Create())
            {
                return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(text)));
            }
        }

        public static byte[] ToBytesFromHexa(string text)
        {
            if (text == null)
                return null;

            var list = new List<byte>();
            bool lo = false;
            byte prev = 0;
            int offset;

            // handle 0x or 0X notation
            if (text.Length >= 2 && text[0] == '0' && (text[1] == 'x' || text[1] == 'X'))
            {
                offset = 2;
            }
            else
            {
                offset = 0;
            }

            for (int i = 0; i < text.Length - offset; i++)
            {
                byte b = GetHexaByte(text[i + offset]);
                if (b == 0xFF)
                    continue;

                if (lo)
                {
                    list.Add((byte)(prev * 16 + b));
                }
                else
                {
                    prev = b;
                }
                lo = !lo;
            }
            return list.ToArray();
        }

        public static byte GetHexaByte(char c)
        {
            if (c >= '0' && c <= '9')
                return (byte)(c - '0');

            if (c >= 'A' && c <= 'F')
                return (byte)(c - 'A' + 10);

            if (c >= 'a' && c <= 'f')
                return (byte)(c - 'a' + 10);

            return 0xFF;
        }

        public static string ToHexa(this byte[] bytes) => bytes != null ? ToHexa(bytes, 0, bytes.Length) : "0x";
        public static string ToHexa(this byte[] bytes, int count) => ToHexa(bytes, 0, count);
        public static string ToHexa(this byte[] bytes, int offset, int count)
        {
            if (bytes == null)
                return "0x";

            if (offset < 0)
                throw new ArgumentException(null, nameof(offset));

            if (count < 0)
                throw new ArgumentException(null, nameof(count));

            if (offset >= bytes.Length)
                throw new ArgumentException(null, nameof(offset));

            count = Math.Min(count, bytes.Length - offset);
            var sb = new StringBuilder(count * 2);
            for (int i = offset; i < (offset + count); i++)
            {
                sb.Append(bytes[i].ToString("X2"));
            }
            return "0x" + sb.ToString();
        }

        public static string ToHexaDump(string text) => ToHexaDump(text, null);
        public static string ToHexaDump(string text, Encoding encoding)
        {
            if (text == null)
                return null;

            if (encoding == null)
            {
                encoding = Encoding.Unicode;
            }

            return ToHexaDump(encoding.GetBytes(text));
        }

        public static string ToHexaDump(this byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            return ToHexaDump(bytes, null);
        }

        public static string ToHexaDump(this byte[] bytes, string prefix)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            return ToHexaDump(bytes, 0, bytes.Length, prefix, true);
        }

        public static string ToHexaDump(this IntPtr ptr, int count) => ToHexaDump(ptr, 0, count, null, true);

        public static string ToHexaDump(this IntPtr ptr, int offset, int count, string prefix, bool addHeader)
        {
            if (ptr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(ptr));

            var bytes = new byte[count];
            Marshal.Copy(ptr, bytes, offset, count);
            return ToHexaDump(bytes, 0, count, prefix, addHeader);
        }

        public static string ToHexaDump(this byte[] bytes, int count) => ToHexaDump(bytes, 0, count, null, true);
        public static string ToHexaDump(this byte[] bytes, int offset, int count) => ToHexaDump(bytes, offset, count, null, true);
        public static string ToHexaDump(this byte[] bytes, int offset, int count, string prefix, bool addHeader)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (offset < 0)
            {
                offset = 0;
            }

            if (count < 0)
            {
                count = bytes.Length;
            }

            if ((offset + count) > bytes.Length)
            {
                count = bytes.Length - offset;
            }

            var sb = new StringBuilder();
            if (addHeader)
            {
                sb.Append(prefix);
                //             0         1         2         3         4         5         6         7
                //             01234567890123456789012345678901234567890123456789012345678901234567890123456789
                sb.AppendLine("Offset    00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F  0123456789ABCDEF");
                sb.AppendLine("--------  -----------------------------------------------  ----------------");
            }

            for (int i = 0; i < count; i += 16)
            {
                sb.Append(prefix);
                sb.AppendFormat("{0:X8}  ", i + offset);

                int j = 0;
                for (j = 0; (j < 16) && ((i + j) < count); j++)
                {
                    sb.AppendFormat("{0:X2} ", bytes[i + j + offset]);
                }

                sb.Append(" ");
                if (j < 16)
                {
                    sb.Append(new string(' ', 3 * (16 - j)));
                }
                for (j = 0; j < 16 && (i + j) < count; j++)
                {
                    var b = bytes[i + j + offset];
                    if (b > 31 && b < 128)
                    {
                        sb.Append((char)b);
                    }
                    else
                    {
                        sb.Append('.');
                    }
                }

                sb.AppendLine();
            }
            return sb.ToString();
        }

        public static List<T> SplitToList<T>(this string text, params char[] separators) => SplitToList<T>(text, null, separators);
        public static List<T> SplitToList<T>(this string text, IFormatProvider provider, params char[] separators)
        {
            var al = new List<T>();
            if (text == null || separators == null || separators.Length == 0)
                return al;

            foreach (string s in text.Split(separators))
            {
                string value = s.Nullify();
                if (value == null)
                    continue;

                var item = ChangeType(value, default(T), provider);
                al.Add(item);
            }
            return al;
        }

        public static string ToNullifiedString(object input, string defaultValue = null, IFormatProvider provider = null)
        {
            if (input == null)
                return defaultValue;

            if (input is string s)
                return s;

            s = string.Format(provider, "{0}", input).Nullify();
            if (s == null)
                return defaultValue;

            return s;
        }

        public static object ChangeType(object input, Type conversionType) => ChangeType(input, conversionType, null, null);
        public static object ChangeType(object input, Type conversionType, object defaultValue) => ChangeType(input, conversionType, defaultValue, null);
        public static object ChangeType(object input, Type conversionType, object defaultValue, IFormatProvider provider)
        {
            if (!TryChangeType(input, conversionType, provider, out object value))
                return defaultValue;

            return value;
        }

        public static T ChangeType<T>(object input) => ChangeType(input, default(T));
        public static T ChangeType<T>(object input, T defaultValue) => ChangeType(input, defaultValue, null);
        public static T ChangeType<T>(object input, T defaultValue, IFormatProvider provider)
        {
            if (!TryChangeType(input, provider, out T value))
                return defaultValue;

            return value;
        }

        public static bool TryChangeType<T>(object input, out T value) => TryChangeType(input, null, out value);
        public static bool TryChangeType<T>(object input, IFormatProvider provider, out T value)
        {
            if (!TryChangeType(input, typeof(T), provider, out object tvalue))
            {
                value = default;
                return false;
            }

            value = (T)tvalue;
            return true;
        }

        public static bool TryChangeType(object input, Type conversionType, out object value) => TryChangeType(input, conversionType, null, out value);
        public static bool TryChangeType(object input, Type conversionType, IFormatProvider provider, out object value)
        {
            if (conversionType == null)
                throw new ArgumentNullException(nameof(conversionType));

            if (conversionType == typeof(object))
            {
                value = input;
                return true;
            }

            if (IsNullable(conversionType))
            {
                if (input == null)
                {
                    value = null;
                    return true;
                }

                Type gType = conversionType.GetGenericArguments()[0];
                if (TryChangeType(input, gType, provider, out object vtValue))
                {
                    var nt = typeof(Nullable<>).MakeGenericType(gType);
                    value = Activator.CreateInstance(nt, vtValue);
                    return true;
                }

                value = null;
                return false;
            }

            value = conversionType.IsValueType ? Activator.CreateInstance(conversionType) : null;
            if (input == null)
                return !conversionType.IsValueType;

            var inputType = input.GetType();
            if (inputType.IsAssignableFrom(conversionType))
            {
                value = input;
                return true;
            }

#if !NO_JSON
            if (input is JsonElement element)
                return TryChangeType(element.ToString(), conversionType, provider, out value);
#endif

            if (conversionType.IsEnum)
                return EnumTryParse(conversionType, input, out value);

            if (inputType.IsEnum)
            {
                var tc = Type.GetTypeCode(inputType);
                if (conversionType == typeof(int))
                {
                    switch (tc)
                    {
                        case TypeCode.Int32:
                            value = (int)input;
                            return true;

                        case TypeCode.Int16:
                            value = (int)(short)input;
                            return true;

                        case TypeCode.Int64:
                            value = (int)(long)input;
                            return true;

                        case TypeCode.UInt32:
                            value = (int)(uint)input;
                            return true;

                        case TypeCode.UInt16:
                            value = (int)(ushort)input;
                            return true;

                        case TypeCode.UInt64:
                            value = (int)(ulong)input;
                            return true;

                        case TypeCode.Byte:
                            value = (int)(byte)input;
                            return true;

                        case TypeCode.SByte:
                            value = (int)(sbyte)input;
                            return true;
                    }
                    return false;
                }

                if (conversionType == typeof(short))
                {
                    switch (tc)
                    {
                        case TypeCode.Int32:
                            value = (short)(int)input;
                            return true;

                        case TypeCode.Int16:
                            value = (short)input;
                            return true;

                        case TypeCode.Int64:
                            value = (short)(long)input;
                            return true;

                        case TypeCode.UInt32:
                            value = (short)(uint)input;
                            return true;

                        case TypeCode.UInt16:
                            value = (short)(ushort)input;
                            return true;

                        case TypeCode.UInt64:
                            value = (short)(ulong)input;
                            return true;

                        case TypeCode.Byte:
                            value = (short)(byte)input;
                            return true;

                        case TypeCode.SByte:
                            value = (short)(sbyte)input;
                            return true;
                    }
                    return false;
                }

                if (conversionType == typeof(long))
                {
                    switch (tc)
                    {
                        case TypeCode.Int32:
                            value = (long)(int)input;
                            return true;

                        case TypeCode.Int16:
                            value = (long)(short)input;
                            return true;

                        case TypeCode.Int64:
                            value = (long)input;
                            return true;

                        case TypeCode.UInt32:
                            value = (long)(uint)input;
                            return true;

                        case TypeCode.UInt16:
                            value = (long)(ushort)input;
                            return true;

                        case TypeCode.UInt64:
                            value = (long)(ulong)input;
                            return true;

                        case TypeCode.Byte:
                            value = (long)(byte)input;
                            return true;

                        case TypeCode.SByte:
                            value = (long)(sbyte)input;
                            return true;
                    }
                    return false;
                }

                if (conversionType == typeof(uint))
                {
                    switch (tc)
                    {
                        case TypeCode.Int32:
                            value = (uint)(int)input;
                            return true;

                        case TypeCode.Int16:
                            value = (uint)(short)input;
                            return true;

                        case TypeCode.Int64:
                            value = (uint)(long)input;
                            return true;

                        case TypeCode.UInt32:
                            value = (uint)input;
                            return true;

                        case TypeCode.UInt16:
                            value = (uint)(ushort)input;
                            return true;

                        case TypeCode.UInt64:
                            value = (uint)(ulong)input;
                            return true;

                        case TypeCode.Byte:
                            value = (uint)(byte)input;
                            return true;

                        case TypeCode.SByte:
                            value = (uint)(sbyte)input;
                            return true;
                    }
                    return false;
                }

                if (conversionType == typeof(ushort))
                {
                    switch (tc)
                    {
                        case TypeCode.Int32:
                            value = (ushort)(int)input;
                            return true;

                        case TypeCode.Int16:
                            value = (ushort)(short)input;
                            return true;

                        case TypeCode.Int64:
                            value = (ushort)(long)input;
                            return true;

                        case TypeCode.UInt32:
                            value = (ushort)(uint)input;
                            return true;

                        case TypeCode.UInt16:
                            value = (ushort)input;
                            return true;

                        case TypeCode.UInt64:
                            value = (ushort)(ulong)input;
                            return true;

                        case TypeCode.Byte:
                            value = (ushort)(byte)input;
                            return true;

                        case TypeCode.SByte:
                            value = (ushort)(sbyte)input;
                            return true;
                    }
                    return false;
                }

                if (conversionType == typeof(ulong))
                {
                    switch (tc)
                    {
                        case TypeCode.Int32:
                            value = (ulong)(int)input;
                            return true;

                        case TypeCode.Int16:
                            value = (ulong)(short)input;
                            return true;

                        case TypeCode.Int64:
                            value = (ulong)(long)input;
                            return true;

                        case TypeCode.UInt32:
                            value = (ulong)(uint)input;
                            return true;

                        case TypeCode.UInt16:
                            value = (ulong)(ushort)input;
                            return true;

                        case TypeCode.UInt64:
                            value = (ulong)input;
                            return true;

                        case TypeCode.Byte:
                            value = (ulong)(byte)input;
                            return true;

                        case TypeCode.SByte:
                            value = (ulong)(sbyte)input;
                            return true;
                    }
                    return false;
                }

                if (conversionType == typeof(byte))
                {
                    switch (tc)
                    {
                        case TypeCode.Int32:
                            value = (byte)(int)input;
                            return true;

                        case TypeCode.Int16:
                            value = (byte)(short)input;
                            return true;

                        case TypeCode.Int64:
                            value = (byte)(long)input;
                            return true;

                        case TypeCode.UInt32:
                            value = (byte)(uint)input;
                            return true;

                        case TypeCode.UInt16:
                            value = (byte)(ushort)input;
                            return true;

                        case TypeCode.UInt64:
                            value = (byte)(ulong)input;
                            return true;

                        case TypeCode.Byte:
                            value = (byte)input;
                            return true;

                        case TypeCode.SByte:
                            value = (byte)(sbyte)input;
                            return true;
                    }
                    return false;
                }

                if (conversionType == typeof(sbyte))
                {
                    switch (tc)
                    {
                        case TypeCode.Int32:
                            value = (sbyte)(int)input;
                            return true;

                        case TypeCode.Int16:
                            value = (sbyte)(short)input;
                            return true;

                        case TypeCode.Int64:
                            value = (sbyte)(long)input;
                            return true;

                        case TypeCode.UInt32:
                            value = (sbyte)(uint)input;
                            return true;

                        case TypeCode.UInt16:
                            value = (sbyte)(ushort)input;
                            return true;

                        case TypeCode.UInt64:
                            value = (sbyte)(ulong)input;
                            return true;

                        case TypeCode.Byte:
                            value = (sbyte)(byte)input;
                            return true;

                        case TypeCode.SByte:
                            value = (sbyte)input;
                            return true;
                    }
                    return false;
                }
            }

            if (conversionType == typeof(Guid))
            {
                string svalue = string.Format(provider, "{0}", input).Nullify();
                if (svalue != null && Guid.TryParse(svalue, out Guid guid))
                {
                    value = guid;
                    return true;
                }
                return false;
            }

            if (conversionType == typeof(Uri))
            {
                string svalue = string.Format(provider, "{0}", input).Nullify();
                if (svalue != null && Uri.TryCreate(svalue, UriKind.RelativeOrAbsolute, out var uri))
                {
                    value = uri;
                    return true;
                }
                return false;
            }

            if (conversionType == typeof(IntPtr))
            {
                if (IntPtr.Size == 8)
                {
                    if (TryChangeType(input, provider, out long l))
                    {
                        value = new IntPtr(l);
                        return true;
                    }
                }
                else if (TryChangeType(input, provider, out int i))
                {
                    value = new IntPtr(i);
                    return true;
                }
                return false;
            }

            if (conversionType == typeof(int))
            {
                if (inputType == typeof(uint))
                {
                    value = unchecked((int)(uint)input);
                    return true;
                }

                if (inputType == typeof(ulong))
                {
                    value = unchecked((int)(ulong)input);
                    return true;
                }

                if (inputType == typeof(ushort))
                {
                    value = unchecked((int)(ushort)input);
                    return true;
                }

                if (inputType == typeof(byte))
                {
                    value = unchecked((int)(byte)input);
                    return true;
                }
            }

            if (conversionType == typeof(long))
            {
                if (inputType == typeof(uint))
                {
                    value = unchecked((long)(uint)input);
                    return true;
                }

                if (inputType == typeof(ulong))
                {
                    value = unchecked((long)(ulong)input);
                    return true;
                }

                if (inputType == typeof(ushort))
                {
                    value = unchecked((long)(ushort)input);
                    return true;
                }

                if (inputType == typeof(byte))
                {
                    value = unchecked((long)(byte)input);
                    return true;
                }

                if (inputType == typeof(TimeSpan))
                {
                    value = ((TimeSpan)input).Ticks;
                    return true;
                }
            }

            if (conversionType == typeof(short))
            {
                if (inputType == typeof(uint))
                {
                    value = unchecked((short)(uint)input);
                    return true;
                }

                if (inputType == typeof(ulong))
                {
                    value = unchecked((short)(ulong)input);
                    return true;
                }

                if (inputType == typeof(ushort))
                {
                    value = unchecked((short)(ushort)input);
                    return true;
                }

                if (inputType == typeof(byte))
                {
                    value = unchecked((short)(byte)input);
                    return true;
                }
            }

            if (conversionType == typeof(sbyte))
            {
                if (inputType == typeof(uint))
                {
                    value = unchecked((sbyte)(uint)input);
                    return true;
                }

                if (inputType == typeof(ulong))
                {
                    value = unchecked((sbyte)(ulong)input);
                    return true;
                }

                if (inputType == typeof(ushort))
                {
                    value = unchecked((sbyte)(ushort)input);
                    return true;
                }

                if (inputType == typeof(byte))
                {
                    value = unchecked((sbyte)(byte)input);
                    return true;
                }
            }

            if (conversionType == typeof(uint))
            {
                if (inputType == typeof(int))
                {
                    value = unchecked((uint)(int)input);
                    return true;
                }

                if (inputType == typeof(long))
                {
                    value = unchecked((uint)(long)input);
                    return true;
                }

                if (inputType == typeof(short))
                {
                    value = unchecked((uint)(short)input);
                    return true;
                }

                if (inputType == typeof(sbyte))
                {
                    value = unchecked((uint)(sbyte)input);
                    return true;
                }
            }

            if (conversionType == typeof(ulong))
            {
                if (inputType == typeof(int))
                {
                    value = unchecked((ulong)(int)input);
                    return true;
                }

                if (inputType == typeof(long))
                {
                    value = unchecked((ulong)(long)input);
                    return true;
                }

                if (inputType == typeof(short))
                {
                    value = unchecked((ulong)(short)input);
                    return true;
                }

                if (inputType == typeof(sbyte))
                {
                    value = unchecked((ulong)(sbyte)input);
                    return true;
                }
            }

            if (conversionType == typeof(ushort))
            {
                if (inputType == typeof(int))
                {
                    value = unchecked((ushort)(int)input);
                    return true;
                }

                if (inputType == typeof(long))
                {
                    value = unchecked((ushort)(long)input);
                    return true;
                }

                if (inputType == typeof(short))
                {
                    value = unchecked((ushort)(short)input);
                    return true;
                }

                if (inputType == typeof(sbyte))
                {
                    value = unchecked((ushort)(sbyte)input);
                    return true;
                }
            }

            if (conversionType == typeof(byte))
            {
                if (inputType == typeof(int))
                {
                    value = unchecked((byte)(int)input);
                    return true;
                }

                if (inputType == typeof(long))
                {
                    value = unchecked((byte)(long)input);
                    return true;
                }

                if (inputType == typeof(short))
                {
                    value = unchecked((byte)(short)input);
                    return true;
                }

                if (inputType == typeof(sbyte))
                {
                    value = unchecked((byte)(sbyte)input);
                    return true;
                }
            }

            if (conversionType == typeof(DateTime))
            {
                if (inputType == typeof(long))
                {
                    value = new DateTime((long)input);
                    return true;
                }

                if (inputType == typeof(DateTimeOffset))
                {
                    value = ((DateTimeOffset)input).DateTime;
                    return true;
                }
            }

            if (conversionType == typeof(DateTimeOffset))
            {
                if (inputType == typeof(long))
                {
                    value = new DateTimeOffset(new DateTime((long)input));
                    return true;
                }

                if (inputType == typeof(DateTime))
                {
                    value = new DateTimeOffset((DateTime)input);
                    return true;
                }
            }

            if (conversionType == typeof(TimeSpan))
            {
                if (inputType == typeof(long))
                {
                    value = new TimeSpan((long)input);
                    return true;
                }

                if (inputType == typeof(DateTime))
                {
                    value = ((DateTime)value).TimeOfDay;
                    return true;
                }

                if (inputType == typeof(DateTimeOffset))
                {
                    value = ((DateTimeOffset)value).TimeOfDay;
                    return true;
                }

                if (TryChangeType(input, provider, out string sv) && TimeSpan.TryParse(sv, provider, out var ts))
                {
                    value = ts;
                    return true;
                }
            }

            bool isGenericList = IsGenericList(conversionType, out var elementType);
            if (conversionType.IsArray || isGenericList)
            {
                if (input is IEnumerable enumerable)
                {
                    if (!isGenericList)
                    {
                        elementType = conversionType.GetElementType();
                    }

                    var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
                    int count = 0;
                    foreach (var obj in enumerable)
                    {
                        count++;
                        if (TryChangeType(obj, elementType, provider, out object elem))
                        {
                            list.Add(elem);
                        }
                    }

                    if (count > 0 && list.Count > 0)
                    {
                        if (isGenericList)
                        {
                            value = list;
                        }
                        else
                        {
                            value = list.GetType().GetMethod(nameof(List<object>.ToArray)).Invoke(list, null);
                        }
                        return true;
                    }
                }
            }

            if (conversionType == typeof(CultureInfo) || conversionType == typeof(IFormatProvider))
            {
                try
                {
                    if (TryChangeType(input, provider, out int lcid))
                    {
                        value = CultureInfo.GetCultureInfo((int)input);
                        return true;
                    }
                    else
                    {
                        if (TryChangeType(input, provider, out string s))
                        {
                            value = CultureInfo.GetCultureInfo(s);
                            return true;
                        }
                    }
                }
                catch
                {
                    // do nothing, wrong culture, etc.
                }
                return false;
            }

            if (conversionType == typeof(bool))
            {
                if (TryChangeType(input, out long bl))
                {
                    value = bl != 0;
                    return true;
                }

                var svalue = string.Format("{0}", provider, value).Nullify();
                if (svalue == null)
                    return false;

                if (svalue.EqualsIgnoreCase("y") || svalue.EqualsIgnoreCase("yes"))
                {
                    value = true;
                    return true;
                }

                if (svalue.EqualsIgnoreCase("n") || svalue.EqualsIgnoreCase("no"))
                {
                    value = false;
                    return true;
                }

                if (!bool.TryParse(svalue, out bool b))
                    return false;

                value = b;
                return true;
            }

            // in general, nothing is convertible to anything but one of these, IConvertible is 100% stupid thing
            bool isWellKnownConvertible()
            {
                return conversionType == typeof(short) || conversionType == typeof(int) ||
                    conversionType == typeof(string) || conversionType == typeof(byte) ||
                    conversionType == typeof(char) || conversionType == typeof(DateTime) ||
                    conversionType == typeof(DBNull) || conversionType == typeof(decimal) ||
                    conversionType == typeof(double) || conversionType.IsEnum ||
                    conversionType == typeof(short) || conversionType == typeof(int) ||
                    conversionType == typeof(long) || conversionType == typeof(sbyte) ||
                    conversionType == typeof(bool) || conversionType == typeof(float) ||
                    conversionType == typeof(ushort) || conversionType == typeof(uint) ||
                    conversionType == typeof(ulong);
            }

            if (isWellKnownConvertible() && input is IConvertible convertible)
            {
                try
                {
                    value = convertible.ToType(conversionType, provider);
                    return true;
                }
                catch
                {
                    // continue;
                }
            }

            if (value != null)
            {
                var converter = TypeDescriptor.GetConverter(value);
                if (converter != null)
                {
                    if (converter.CanConvertTo(conversionType))
                    {
                        try
                        {
                            value = converter.ConvertTo(null, provider as CultureInfo, input, conversionType);
                            return true;
                        }
                        catch
                        {
                            // continue;
                        }
                    }

                    if (converter.CanConvertFrom(inputType))
                    {
                        try
                        {
                            value = converter.ConvertFrom(null, provider as CultureInfo, input);
                            return true;
                        }
                        catch
                        {
                            // continue;
                        }
                    }
                }
            }

            if (input != null)
            {
                var converter = TypeDescriptor.GetConverter(input);
                if (converter != null)
                {
                    if (converter.CanConvertTo(conversionType))
                    {
                        try
                        {
                            value = converter.ConvertTo(null, provider as CultureInfo, input, conversionType);
                            return true;
                        }
                        catch
                        {
                            // continue;
                        }
                    }
                }
            }

            if (conversionType == typeof(string))
            {
                value = string.Format(provider, "{0}", input);
                return true;
            }

            return false;
        }

        public static ulong EnumToUInt64(string text, Type enumType)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            return EnumToUInt64(ChangeType(text, enumType));
        }

        public static ulong EnumToUInt64(object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var typeCode = Convert.GetTypeCode(value);
            switch (typeCode)
            {
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                    return (ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture);

                case TypeCode.Byte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return Convert.ToUInt64(value, CultureInfo.InvariantCulture);

                case TypeCode.String:
                default:
                    return ChangeType<ulong>(value, 0, CultureInfo.InvariantCulture);
            }
        }

        private static bool StringToEnum(Type type, Type underlyingType, string[] names, Array values, string input, out object value)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].EqualsIgnoreCase(input))
                {
                    value = values.GetValue(i);
                    return true;
                }
            }

            for (int i = 0; i < values.GetLength(0); i++)
            {
                object valuei = values.GetValue(i);
                if (input.Length > 0 && input[0] == '-')
                {
                    var ul = (long)EnumToUInt64(valuei);
                    if (ul.ToString().EqualsIgnoreCase(input))
                    {
                        value = valuei;
                        return true;
                    }
                }
                else
                {
                    var ul = EnumToUInt64(valuei);
                    if (ul.ToString().EqualsIgnoreCase(input))
                    {
                        value = valuei;
                        return true;
                    }
                }
            }

            if (char.IsDigit(input[0]) || input[0] == '-' || input[0] == '+')
            {
                var obj = EnumToObject(type, input);
                if (obj == null)
                {
                    value = Activator.CreateInstance(type);
                    return false;
                }
                value = obj;
                return true;
            }

            value = Activator.CreateInstance(type);
            return false;
        }

        public static int GetEnumMaxPower(Type enumType)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            if (!enumType.IsEnum)
                throw new ArgumentException(null, nameof(enumType));

            return GetEnumUnderlyingTypeMaxPower(Enum.GetUnderlyingType(enumType));
        }

        public static int GetEnumUnderlyingTypeMaxPower(Type underlyingType)
        {
            if (underlyingType == null)
                throw new ArgumentNullException(nameof(underlyingType));

            if (underlyingType == typeof(long) || underlyingType == typeof(ulong))
                return 64;

            if (underlyingType == typeof(int) || underlyingType == typeof(uint))
                return 32;

            if (underlyingType == typeof(short) || underlyingType == typeof(ushort))
                return 16;

            if (underlyingType == typeof(byte) || underlyingType == typeof(sbyte))
                return 8;

            throw new ArgumentException(null, nameof(underlyingType));
        }

        public static object EnumToObject(Type enumType, object value)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            if (!enumType.IsEnum)
                throw new ArgumentException(null, nameof(enumType));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var underlyingType = Enum.GetUnderlyingType(enumType);
            if (underlyingType == typeof(long))
                return Enum.ToObject(enumType, ChangeType<long>(value));

            if (underlyingType == typeof(ulong))
                return Enum.ToObject(enumType, ChangeType<ulong>(value));

            if (underlyingType == typeof(int))
                return Enum.ToObject(enumType, ChangeType<int>(value));

            if ((underlyingType == typeof(uint)))
                return Enum.ToObject(enumType, ChangeType<uint>(value));

            if (underlyingType == typeof(short))
                return Enum.ToObject(enumType, ChangeType<short>(value));

            if (underlyingType == typeof(ushort))
                return Enum.ToObject(enumType, ChangeType<ushort>(value));

            if (underlyingType == typeof(byte))
                return Enum.ToObject(enumType, ChangeType<byte>(value));

            if (underlyingType == typeof(sbyte))
                return Enum.ToObject(enumType, ChangeType<sbyte>(value));

            throw new ArgumentException(null, nameof(enumType));
        }

        public static object ToEnum(object obj, Enum defaultValue)
        {
            if (defaultValue == null)
                throw new ArgumentNullException(nameof(defaultValue));

            if (obj == null)
                return defaultValue;

            if (obj.GetType() == defaultValue.GetType())
                return obj;

            if (EnumTryParse(defaultValue.GetType(), obj.ToString(), out object value))
                return value;

            return defaultValue;
        }

        public static object ToEnum(string text, Type enumType)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            EnumTryParse(enumType, text, out object value);
            return value;
        }

        public static Enum ToEnum(string text, Enum defaultValue)
        {
            if (defaultValue == null)
                throw new ArgumentNullException(nameof(defaultValue));

            if (EnumTryParse(defaultValue.GetType(), text, out object value))
                return (Enum)value;

            return defaultValue;
        }

        public static bool EnumTryParse(Type type, object input, out object value)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (!type.IsEnum)
                throw new ArgumentException(null, nameof(type));

            if (input == null)
            {
                value = Activator.CreateInstance(type);
                return false;
            }

            var stringInput = string.Format(CultureInfo.InvariantCulture, "{0}", input);
            stringInput = stringInput.Nullify();
            if (stringInput == null)
            {
                value = Activator.CreateInstance(type);
                return false;
            }

            if (stringInput.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (ulong.TryParse(stringInput.Substring(2), NumberStyles.HexNumber, null, out ulong ulx))
                {
                    value = ToEnum(ulx.ToString(CultureInfo.InvariantCulture), type);
                    return true;
                }
            }

            var names = Enum.GetNames(type);
            if (names.Length == 0)
            {
                value = Activator.CreateInstance(type);
                return false;
            }

            var underlyingType = Enum.GetUnderlyingType(type);
            var values = Enum.GetValues(type);
            // some enums like System.CodeDom.MemberAttributes *are* flags but are not declared with Flags...
            if (!type.IsDefined(typeof(FlagsAttribute), true) && stringInput.IndexOfAny(_enumSeparators) < 0)
                return StringToEnum(type, underlyingType, names, values, stringInput, out value);

            // multi value enum
            var tokens = stringInput.Split(_enumSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                value = Activator.CreateInstance(type);
                return false;
            }

            ulong ul = 0;
            foreach (string tok in tokens)
            {
                string token = tok.Nullify(); // NOTE: we don't consider empty tokens as errors
                if (token == null)
                    continue;

                if (!StringToEnum(type, underlyingType, names, values, token, out object tokenValue))
                {
                    value = Activator.CreateInstance(type);
                    return false;
                }

                ulong tokenUl;
                switch (Convert.GetTypeCode(tokenValue))
                {
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                    case TypeCode.SByte:
                        tokenUl = (ulong)Convert.ToInt64(tokenValue, CultureInfo.InvariantCulture);
                        break;

                    default:
                        tokenUl = Convert.ToUInt64(tokenValue, CultureInfo.InvariantCulture);
                        break;
                }

                ul |= tokenUl;
            }
            value = Enum.ToObject(type, ul);
            return true;
        }

        public static bool IsGenericList(Type type, out Type elementType)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }

            elementType = null;
            return false;
        }

        public static bool IsNullable(this Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public static string FormatByteSize(long size)
        {
            var sb = new StringBuilder(64);
            StrFormatByteSizeW(size, sb, sb.Capacity);
            return sb.ToString();
        }

        [DllImport("shlwapi", CharSet = CharSet.Unicode)]
        private static extern long StrFormatByteSizeW(long qdw, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszBuf, int cchBuf);

#if !NO_JSON
        public static string GetNullifiedValue(this JsonElement element, string jsonPath) => GetValue<string>(element, jsonPath, null).Nullify();
        public static string GetNullifiedValue(this IDictionary<string, JsonElement> element, string key, string defaultValue = null, IFormatProvider provider = null)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (element == null)
                return defaultValue.Nullify();

            if (!element.TryGetValue(key, out var o))
                return defaultValue;

            return ChangeType(o, defaultValue, provider).Nullify();
        }

        public static T GetValue<T>(this JsonElement element, string jsonPath, T defaultValue = default)
        {
            if (!TryGetValue(element, jsonPath, out T value))
                return defaultValue;

            return value;
        }

        public static bool TryGetValue<T>(this JsonElement element, string jsonPath, out T value)
        {
            if (!TryGetValue(element, jsonPath, out object obj))
            {
                value = default;
                return false;
            }

            return TryChangeType(obj, out value);
        }

        public static bool TryGetValue(this JsonElement element, string jsonPath, out object value)
        {
            if (jsonPath == null)
                throw new ArgumentNullException(nameof(jsonPath));

            if (element.TryGetProperty(jsonPath, out var directElement))
            {
                value = directElement.ToObject();
                return true;
            }

            value = null;
            var segments = jsonPath.Split('.');
            var current = element;
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i].Nullify();
                if (segment == null)
                    return false;

                if (!current.TryGetProperty(segment, out var newElement))
                    return false;

                if (i == segments.Length - 1)
                {
                    value = newElement.ToObject();
                    return true;
                }
                current = newElement;
            }
            return false;
        }

        public static object ToObject(this JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Null:
                    return null;

                case JsonValueKind.Object:
                    var dic = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    foreach (var child in element.EnumerateObject())
                    {
                        dic[child.Name] = ToObject(child.Value);
                    }
                    return dic;

                case JsonValueKind.Array:
                    var objects = new object[element.GetArrayLength()];
                    var i = 0;
                    foreach (var child in element.EnumerateArray())
                    {
                        objects[i++] = ToObject(child);
                    }
                    return objects;

                case JsonValueKind.String:
                    var str = element.ToString();
                    if (DateTime.TryParseExact(str, new string[] { "o", "r", "s" }, null, DateTimeStyles.None, out var dt))
                        return dt;

                    return str;

                case JsonValueKind.Number:
                    if (element.TryGetInt32(out var i32))
                        return i32;

                    if (element.TryGetInt32(out var i64))
                        return i64;

                    if (element.TryGetDecimal(out var dec))
                        return dec;

                    if (element.TryGetDouble(out var dbl))
                        return dbl;

                    throw new NotSupportedException();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                default:
                    throw new NotSupportedException();
            }
        }
#endif
    }
}
