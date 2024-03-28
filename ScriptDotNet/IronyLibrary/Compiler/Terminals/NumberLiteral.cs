#region License
/* **********************************************************************************
 * Copyright (c) Roman Ivantsov
 * This source code is subject to terms and conditions of the MIT License
 * for Irony. A copy of the license can be found in the License.txt file
 * at the root of this distribution. 
 * By using this source code in any fashion, you are agreeing to be bound by the terms of the 
 * MIT License.
 * You must not remove this notice from this software.
 * **********************************************************************************/
#endregion
//Authors: Roman Ivantsov - initial implementation and some later edits
//         Philipp Serr - implementation of advanced features for c#, python, VB

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Diagnostics;

namespace Irony.Compiler {
  using BigInteger = Microsoft.Scripting.Math.BigInteger;
  using Complex64 = Microsoft.Scripting.Math.Complex64;


  //TODO: For VB, we may need to add a flag to automatically use long instead of int (default) when number is too large
  public class NumberLiteral : CompoundTerminalBase {
    #region Public Consts
    //currently CompoundTerminalBase relies on TypeCode
    public const TypeCode TypeCodeBigInt = (TypeCode)30;
    public const TypeCode TypeCodeImaginary = (TypeCode)31;
    #endregion

    #region constructors
    public NumberLiteral(string name, TermOptions options)  : this(name) {
      SetOption(options);
    }
    public NumberLiteral(string name)  : base(name) {
      base.MatchMode = TokenMatchMode.ByType;
    }
    public NumberLiteral(string name, string displayName)  : this(name) {
      base.DisplayName = displayName;
    }
    #endregion

    #region Public fields/properties: ExponentSymbols, Suffixes
    public string QuickParseTerminators;
    public string ExponentSymbols = "eE"; //most of the time; in some languages (Scheme) we have more
    public char DecimalSeparator = '.';

    //Default types are assigned to literals without suffixes; first matching type used
    public TypeCode[] DefaultIntTypes = new TypeCode[] { TypeCode.Int32 };
    public TypeCode DefaultFloatType = TypeCode.Double;
    private TypeCode[] _defaultFloatTypes;
    #endregion

    #region Private fields: _quickParseTerminators
    #endregion

    #region overrides
    public override void Init(Grammar grammar) {
      base.Init(grammar);
      if (string.IsNullOrEmpty(QuickParseTerminators))  
        QuickParseTerminators = grammar.WhitespaceChars + grammar.Delimiters;
      _defaultFloatTypes = new TypeCode[] { DefaultFloatType };
    }
    public override IList<string> GetFirsts() {
      StringList result = new StringList();
      result.AddRange(base.Prefixes);
      //we assume that prefix is always optional, so number can always start with plain digit
      result.AddRange(new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" });
      // Python float numbers can start with a dot
      if (IsSet(TermOptions.NumberAllowStartEndDot))
        result.Add(DecimalSeparator.ToString());
      return result;
    }

    //Most numbers in source programs are just one-digit instances of 0, 1, 2, and maybe others until 9
    // so we try to do a quick parse for these, without starting the whole general process
    protected override Token QuickParse(CompilerContext context, ISourceStream source) {
      char current = source.CurrentChar;
      if (char.IsDigit(current) && QuickParseTerminators.IndexOf(source.NextChar) >= 0) {
        int iValue = current - '0';
        object value = null;
        switch (DefaultIntTypes[0]) {
          case TypeCode.Int32: value = iValue; break;
          case TypeCode.UInt32: value = (UInt32)iValue; break;
          case TypeCode.Byte: value = (byte)iValue; break;
          case TypeCode.SByte: value = (sbyte) iValue; break;
          case TypeCode.Int16: value = (Int16)iValue; break;
          case TypeCode.UInt16: value = (UInt16)iValue; break;
          default: return null; 
        }
        Token token = Token.Create(this, context, source.TokenStart, current.ToString(), value);
        source.Position++;
        return token;
      } else
        return null;
    }

    protected override void ReadPrefix(ISourceStream source, ScanDetails details) {
      //check that is not a  0 followed by dot; 
      //this may happen in Python for number "0.123" - we can mistakenly take "0" as octal prefix
      if (source.CurrentChar == '0' && source.NextChar == '.') return;
      base.ReadPrefix(source, details);
    }//method

    protected override void ReadSuffix(ISourceStream source, ScanDetails details) {
      base.ReadSuffix(source, details);
      if (string.IsNullOrEmpty(details.Suffix))
        details.TypeCodes = details.IsSet(ScanFlags.HasDotOrExp) ? _defaultFloatTypes : DefaultIntTypes;
    }

    protected override bool ReadBody(ISourceStream source, ScanDetails details) {
      //remember start - it may be different from source.TokenStart, we may have skipped 
      int start = source.Position;
      //Figure out digits set
      string digits = GetDigits(details);
      bool isDecimal = !details.IsSet(ScanFlags.NonDecimal);
      bool allowFloat = !IsSet(TermOptions.NumberIntOnly);

      while (!source.EOF()) {
        char current = source.CurrentChar;
        //1. If it is a digit, just continue going
        if (digits.IndexOf(current) >= 0) {
          source.Position++;
          continue;
        }
        //2. Check if it is a dot
        if (current == DecimalSeparator && allowFloat) {
          //If we had seen already a dot or exponent, don't accept this one;
          //In python number literals (NumberAllowPointFloat) a point can be the first and last character,
          //otherwise we accept dot only if it is followed by a digit
          if (details.IsSet(ScanFlags.HasDotOrExp) || (digits.IndexOf(source.NextChar) < 0) && !IsSet(TermOptions.NumberAllowStartEndDot))
            break; //from while loop
          details.Flags |= ScanFlags.HasDot;
          source.Position++;
          continue;
        }
        //3. Only for decimals - check if it is (the first) exponent symbol
        if (allowFloat && isDecimal && (details.ControlSymbol == null) && (ExponentSymbols.IndexOf(current) >= 0)) {
          char next = source.NextChar;
          bool nextIsSign = next == '-' || next == '+';
          bool nextIsDigit = digits.IndexOf(next) >= 0;
          if (!nextIsSign && !nextIsDigit)
            break;  //Exponent should be followed by either sign or digit
          //ok, we've got real exponent
          details.ControlSymbol = current.ToString(); //remember the exp char
          details.Flags |= ScanFlags.HasExp;
          source.Position++;
          if (nextIsSign)
            source.Position++; //skip +/- explicitly so we don't have to deal with them on the next iteration
          continue;
        }
        //4. It is something else (not digit, not dot or exponent) - we're done
        break; //from while loop
      }//while
      int end = source.Position;
      details.Body = source.Text.Substring(start, end - start);
      return true;
    }

    protected override bool ConvertValue(ScanDetails details) {
      if (String.IsNullOrEmpty(details.Body)) {
        details.Error = "Invalid number.";
        return false;
      }
      //base method fires event and lets custom code convert the value; if it returns true, the value was converted.
      if (base.ConvertValue(details))
        return true; 

      //Try quick paths
      switch (details.TypeCodes[0]) {
        case TypeCode.Int32: 
          if (QuickConvertToInt32(details)) return true;
          break;
        case TypeCode.Double:
          if (QuickConvertToDouble(details)) return true;
          break;
      }

      //Go full cycle
      details.Value = null;
      foreach (TypeCode typeCode in details.TypeCodes) {
        switch (typeCode) {
          case TypeCode.Single:   case TypeCode.Double:  case TypeCode.Decimal:  case TypeCodeImaginary:
            return ConvertToFloat(typeCode, details);
          case TypeCode.SByte:    case TypeCode.Byte:    case TypeCode.Int16:    case TypeCode.UInt16:
          case TypeCode.Int32:    case TypeCode.UInt32:  case TypeCode.Int64:    case TypeCode.UInt64:
            if (details.Value == null) //if it is not done yet
              TryConvertToUlong(details); //try to convert to ULong and place the result into details.Value field;
            if(TryCastToIntegerType(typeCode, details)) //now try to cast the ULong value to the target type 
              return true;
            break;
          case TypeCodeBigInt:
            if (ConvertToBigInteger(details)) return true;
            break; 
        }//switch
      }
      return false; 
    }//method

    #endregion

    #region private utilities
    private bool QuickConvertToInt32(ScanDetails details) {
      TypeCode type = details.TypeCodes[0];
      int radix = GetRadix(details);
      if (radix == 10 && details.Body.Length > 10) return false;    //10 digits is maximum for int32; int32.MaxValue = 2 147 483 647
      try {
        //workaround for .Net FX bug: http://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=278448
        if (radix == 10)
          details.Value = Convert.ToInt32(details.Body, CultureInfo.InvariantCulture);
        else
          details.Value = Convert.ToInt32(details.Body, radix);
        return true;
      } catch {
        return false;
      }
    }//method

    private bool QuickConvertToDouble(ScanDetails details) {
      if (details.IsSet(ScanFlags.Binary | ScanFlags.Octal | ScanFlags.Hex | ScanFlags.HasExp)) return false; 
      if (DecimalSeparator != '.') return false;
      double result;
#if PocketPC || SILVERLIGHT
      try
      {
        result = Convert.ToDouble(details.Body, CultureInfo.InvariantCulture);
      }
      catch
      {
        return false;
      }
#else
      if (!double.TryParse(details.Body, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out result)) return false;
#endif
      details.Value = result;
      return true; 
    }
    private bool ConvertToFloat(TypeCode typeCode, ScanDetails details) {
      //only decimal numbers can be fractions
      if (details.IsSet(ScanFlags.Binary | ScanFlags.Octal | ScanFlags.Hex))
      {
        details.Error = "Invalid number.";
        return false;
      }
      string body = details.Body;
      //Some languages allow exp symbols other than E. Check if it is the case, and change it to E
      // - otherwise .NET conversion methods may fail
      if (details.IsSet(ScanFlags.HasExp) && details.ControlSymbol.ToUpper() != "E")
        body = body.Replace(details.ControlSymbol, "E");

      //'.' decimal seperator required by invariant culture
      if (details.IsSet(ScanFlags.HasDot) && DecimalSeparator != '.')
        body = body.Replace(DecimalSeparator, '.');

      switch (typeCode)
      {
        case TypeCode.Double:
        case TypeCodeImaginary:
          double dValue;
#if PocketPC || SILVERLIGHT
          try
          {
            dValue = Convert.ToDouble(body, CultureInfo.InvariantCulture);
          }
          catch
          {
            return false;
          }
#else
          if (!Double.TryParse(body, NumberStyles.Float, CultureInfo.InvariantCulture, out dValue)) return false;
#endif
          if (typeCode == TypeCodeImaginary)
            details.Value = new Complex64(0, dValue);
          else
            details.Value = dValue;
          return true;
        case TypeCode.Single:
          float fValue;
#if PocketPC || SILVERLIGHT
          try
          {
            fValue = Convert.ToSingle(body, CultureInfo.InvariantCulture);
          }
          catch
          {
            return false;
          }

#else
              if (!Single.TryParse(body, NumberStyles.Float, CultureInfo.InvariantCulture, out fValue)) return false;
#endif
          details.Value = fValue;
          return true;
        case TypeCode.Decimal:
          decimal decValue;
#if PocketPC || SILVERLIGHT
          try
          {
            decValue = Convert.ToDecimal(body, CultureInfo.InvariantCulture);
          }
          catch
          {
            return false;
          }
#else
          if (!Decimal.TryParse(body, NumberStyles.Float, CultureInfo.InvariantCulture, out decValue)) return false;
#endif
          details.Value = decValue;
          return true;
      }//switch
      return false; 
    }
    private bool TryCastToIntegerType(TypeCode typeCode, ScanDetails details) {
      if (details.Value == null) return false;
      try {
        if (typeCode != TypeCode.UInt64)
          details.Value = Convert.ChangeType(details.Value, typeCode, CultureInfo.InvariantCulture);
        return true;
      } catch (Exception e) {
#if !SILVERLIGHT
        Trace.WriteLine("Error converting to integer: text=[" + details.Body + "], type=" + typeCode + ", error: " + e.Message); 
#endif
        return false;
      }
    }//method

    private bool TryConvertToUlong(ScanDetails details) {
      try {
        int radix = GetRadix(details);
        //workaround for .Net FX bug: http://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=278448
        if (radix == 10)
          details.Value = Convert.ToUInt64(details.Body, CultureInfo.InvariantCulture);
        else
          details.Value = Convert.ToUInt64(details.Body, radix);
        return true; 
      } catch(OverflowException) {
        return false;
      }
    }


    private bool ConvertToBigInteger(ScanDetails details) {
      //ignore leading zeros
      details.Body = details.Body.TrimStart('0');
      int bodyLength = details.Body.Length;
      int radix = GetRadix(details);
      int wordLength = GetSafeWordLength(details);
      int sectionCount = GetSectionCount(bodyLength, wordLength);
      ulong[] numberSections = new ulong[sectionCount]; //big endian

      try {
        int startIndex = details.Body.Length - wordLength;
        for (int sectionIndex = sectionCount - 1; sectionIndex >= 0; sectionIndex--) {
          if (startIndex < 0) {
            wordLength += startIndex;
            startIndex = 0;
          }
          //workaround for .Net FX bug: http://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=278448
          if (radix == 10)
            numberSections[sectionIndex] = Convert.ToUInt64(details.Body.Substring(startIndex, wordLength));
          else
            numberSections[sectionIndex] = Convert.ToUInt64(details.Body.Substring(startIndex, wordLength), radix);

          startIndex -= wordLength;
        }
      } catch {
        details.Error = "Invalid number.";
        return false;
      }
      //produce big integer
      ulong safeWordRadix = GetSafeWordRadix(details);
      BigInteger bigIntegerValue = numberSections[0];
      for (int i = 1; i < sectionCount; i++)
        bigIntegerValue = checked(bigIntegerValue * safeWordRadix + numberSections[i]);
      details.Value = bigIntegerValue;
      return true;
    }

    private int GetRadix(ScanDetails details) {
      if (details.IsSet(ScanFlags.Hex))
        return 16;
      if (details.IsSet(ScanFlags.Octal))
        return 8;
      if (details.IsSet(ScanFlags.Binary))
        return 2;
      return 10;
    }
    private string GetDigits(ScanDetails details) {
      if (details.IsSet(ScanFlags.Hex))
        return TextUtils.HexDigits;
      if (details.IsSet(ScanFlags.Octal))
        return TextUtils.OctalDigits;
      if (details.IsSet(ScanFlags.Binary))
        return TextUtils.BinaryDigits;
      return TextUtils.DecimalDigits;
    }
    private int GetSafeWordLength(ScanDetails details) {
      if (details.IsSet(ScanFlags.Hex))
        return 15;
      if (details.IsSet(ScanFlags.Octal))
        return 21; //maxWordLength 22
      if (details.IsSet(ScanFlags.Binary))
        return 63;
      return 19; //maxWordLength 20
    }
    private int GetSectionCount(int stringLength, int safeWordLength) {
      int remainder;
#if PocketPC || SILVERLIGHT
      int quotient = DivRem(stringLength, safeWordLength, out remainder);
#else
      int quotient = Math.DivRem(stringLength, safeWordLength, out remainder);
#endif
      return remainder == 0 ? quotient : quotient + 1;
    }

#if PocketPC || SILVERLIGHT
    public static int DivRem(int a, int b, out int result)
    {
      result = a % b;
      return (a / b);
    }
#endif

    //radix^safeWordLength
    private ulong GetSafeWordRadix(ScanDetails details) {
      if (details.IsSet(ScanFlags.Hex))
        return 1152921504606846976;
      if (details.IsSet(ScanFlags.Octal))
        return 9223372036854775808;
      if (details.IsSet(ScanFlags.Binary))
        return 9223372036854775808;
      return 10000000000000000000;
    }
    private static bool IsIntegerCode(TypeCode code) {
      return (code >= TypeCode.SByte && code <= TypeCode.UInt64);
    }
    #endregion


  }//class


}
