using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CSVSplitter;
using CsvHelper;
using CsvHelper.Configuration;

namespace CSVSplitter.Models
{
    public class InputFile : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public string _filePath;

        public string FilePath 
        {
            get
            {
                return _filePath;
            }
            set
            {
                _filePath = value;
                NotifyPropertyChanged();
            }
        }

        private bool _isTextFile = false;

        private char _delimiter;

        private bool _hasBom;

        private bool _isAnalyzed = false;

        private Encoding _encoding = null;

        private string _newLine = null;

        private string _rawHeader;

        private string[] _header;

        private bool _isCsvFile = false;

        private bool _hasDoubleQuote = false;

        public char Delimiter
        {
            get
            {
                return _delimiter;
            }
            private set
            {
                _delimiter = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("DelimiterName");
            }
        }

        public string DelimiterName
        {
            get
            {
                if (this.Delimiter == '\t')
                {
                    return "タブ";
                }
                else if (this.Delimiter == ',')
                {
                    return "カンマ( , )";
                }
                else if (this.Delimiter == ';')
                {
                    return "セミコロン( ; )";
                }
                else if (this.Delimiter == '|')
                {
                    return "パイプ( | )";
                }
                else
                {
                    return this.Delimiter.ToString();
                }
            }
        }


        public bool HasBom
        {
            get
            {
                return _hasBom;
            }
            private set
            {
                _hasBom = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("HasBomName");
            }
        }

        public string HasBomName
        {
            get
            {
                return this.HasBom ? "あり" : "なし";
            }
        }

        public bool IsAnalyzed
        { 
            get 
            { 
                return _isAnalyzed;
            }
            private set
            {
                _isAnalyzed = value;
                NotifyPropertyChanged();
            }
        }

        public bool IsTextFile
        {
            get
            {
                return _isTextFile;
            }
            private set
            {
                _isTextFile = value;
                NotifyPropertyChanged();
            }
        }

        public Encoding Encoding
        {
            get
            {
                return _encoding;
            }
            private set
            {
                _encoding = value;
                NotifyPropertyChanged();
            }
        }

        public string NewLine
        {
            get
            {
                return _newLine;
            }
            private set
            {
                _newLine = value;
                NotifyPropertyChanged();
            }
        }

        public string RawHeader
        {
            get
            {
                return _rawHeader;
            }
            private set
            {
                _rawHeader = value;
                NotifyPropertyChanged();
            }
        }

        public string[] Header
        {
            get
            {
                return _header;
            }
            private set
            {
                _header = value;
                NotifyPropertyChanged();
            }
        }

        public bool IsCsvFile
        {
            get
            {
                return _isCsvFile;
            }
            private set
            {
                _isCsvFile = value;
                NotifyPropertyChanged();
            }
        }

        public bool HasDoubleQuote
        {
            get
            {
                return _hasDoubleQuote;
            }
            private set
            {
                _hasDoubleQuote = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("HasDoubleQuoteName");
            }
        }

        public string HasDoubleQuoteName
        {
            get
            {
                return this.HasDoubleQuote ? "あり" : "なし";
            }
        }

        public void Analyze()
        {
            this.IsCsvFile = false;
            this.IsTextFile = false;
            this.Encoding = null;
            this.NewLine = null;
            this.RawHeader = null;
            this.Header = new string[0];
            this.Delimiter = '\0';
            this.HasBom = false;
            this.HasDoubleQuote = false;

            if (!File.Exists(this.FilePath))
            {
                this.IsAnalyzed = false;
                return;
            }

            var fileInfo = new FileInfo(this.FilePath);
            if (fileInfo.Length == 0)
            {
                this.IsAnalyzed = true;
                return;
            }

            this.Encoding = GetEncoding(this.FilePath);
            if (this._encoding is null)
            {
                this.IsAnalyzed = true;
                return;
            }

            this.IsTextFile = true;

            using (var fs = new FileStream(this.FilePath, FileMode.Open))
            {
                byte[] buffer = new byte[4];
                fs.Read(buffer, 0, buffer.Length);
                this.HasBom = CheckBom(buffer);
            }

            this.Encoding = ApplyBomSetting(this.Encoding, this.HasBom);

            using (var reader = new StreamReader(this.FilePath, this.Encoding))
            {
                this.Delimiter = '\0';

                // とりあえず、一行目を読み込む
                var header = reader.ReadLine();
                this.RawHeader = header;
                if (header is null)
                {
                    this.IsTextFile = false;
                    this.IsAnalyzed = true;
                    return;
                }

                int conmaCount = header.Count(c => c == ',');
                int tabCount = header.Count(c => c == '\t');

                if (conmaCount > tabCount)
                {
                    this.Delimiter = ',';
                }
                else if (conmaCount < tabCount)
                {
                    this.Delimiter = '\t';
                }
            }

            // 改行コードの判定をする
            using (var reader = new StreamReader(this.FilePath, this.Encoding))
            {
                const int maxReadSize = 512 * 1024;
                int readSize = 0;

                const char CR = '\r';
                const char LF = '\n';
                char nowChar = (char)0;
                char firstChar = (char)0;
                bool readedFirstChar = false;
                bool dqFlag = false;
                this.NewLine = null;
                while (!reader.EndOfStream)
                {
                    char nextChar = (char)reader.Read();
                    if (!readedFirstChar)
                    {
                        firstChar = nextChar;
                        readedFirstChar = true;
                    }

                    if (firstChar == '"')
                    {
                        if (nextChar == '"')
                        {
                            dqFlag = !dqFlag;
                        }
                        if (nowChar == CR && nextChar == LF)
                        {
                            this.NewLine = CR.ToString() + LF.ToString();
                            if (!dqFlag)
                            {
                                break;
                            }
                        }
                        else if (nowChar == CR)
                        {
                            this.NewLine = CR.ToString();
                            if (!dqFlag)
                            {
                                break;
                            }
                        }
                        else if (nowChar == LF)
                        {
                            this.NewLine = LF.ToString();
                            if (!dqFlag)
                            {
                                break;
                            }
                        }
                        nowChar = nextChar;
                        readSize++;
                        if (readSize > maxReadSize)
                        {
                            break;
                        }
                    }
                    else
                    {
                        if (nowChar == CR && nextChar == LF)
                        {
                            this.NewLine = CR.ToString() + LF.ToString();
                            break;
                        }
                        else if (nowChar == CR)
                        {
                            this.NewLine = CR.ToString();
                            break;
                        }
                        else if (nowChar == LF)
                        {
                            this.NewLine = LF.ToString();
                            break;
                        }
                        nowChar = nextChar;
                        readSize++;
                        if (readSize > maxReadSize)
                        {
                            break;
                        }
                    }
                }
                this.HasDoubleQuote = firstChar == '"';
            }

            this.IsCsvFile = this.IsTextFile && this.Delimiter != '\0' && this.NewLine != null && this.Encoding != null;
            this.Header = GetColName().ToArray();

            this.IsAnalyzed = true;

        }

        private System.Text.Encoding GetEncoding(string filename)
        {
            const int maxSize = 512 * 1024;
            var file = new System.IO.FileInfo(filename);
            var readSize = (int)Math.Min(maxSize, file.Length);

            if (readSize <= 0)
            {
                return null;
            }

            byte[] buffer = new byte[readSize];
            using (var fs = file.OpenRead())
            {
                fs.Read(buffer, 0, buffer.Length);
            }

            if (buffer.Length >= 4)
            {
                if (buffer[0] == 0x00 && buffer[1] == 0x00 && buffer[2] == 0xFE && buffer[3] == 0xFF)
                {
                    return new System.Text.UTF32Encoding(true, true);
                }

                if (buffer[0] == 0xFF && buffer[1] == 0xFE && buffer[2] == 0x00 && buffer[3] == 0x00)
                {
                    return new System.Text.UTF32Encoding(false, true);
                }
            }

            if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            {
                return new System.Text.UTF8Encoding(true);
            }

            if (buffer.Length >= 2)
            {
                if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                {
                    return new System.Text.UnicodeEncoding(true, true);
                }

                if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                {
                    return new System.Text.UnicodeEncoding(false, true);
                }
            }

            var allowIncompleteTail = file.Length > readSize;
            if (TryDetectUtf16WithoutBom(buffer, out var utf16Encoding))
            {
                return utf16Encoding;
            }

            if (IsLikelyIso2022Jp(buffer, allowIncompleteTail))
            {
                return System.Text.Encoding.GetEncoding("iso-2022-jp");
            }

            if (IsValidUtf8(buffer, allowIncompleteTail))
            {
                return new System.Text.UTF8Encoding(false);
            }

            if (IsValidEucJp(buffer, allowIncompleteTail))
            {
                return System.Text.Encoding.GetEncoding("euc-jp");
            }

            if (!IsLikelyTextContent(buffer))
            {
                return null;
            }

            if (IsValidCp932(buffer, allowIncompleteTail))
            {
                return System.Text.Encoding.GetEncoding(932);
            }

            return null;
        }

        private bool TryDetectUtf16WithoutBom(byte[] buffer, out System.Text.Encoding encoding)
        {
            encoding = null;

            if (buffer.Length < 4)
            {
                return false;
            }

            int sampleLength = Math.Min(buffer.Length, 8192);
            sampleLength -= sampleLength % 2;
            if (sampleLength < 4)
            {
                return false;
            }

            double littleEndianScore = ScoreUtf16WithoutBom(buffer, sampleLength, false);
            double bigEndianScore = ScoreUtf16WithoutBom(buffer, sampleLength, true);
            double bestScore = Math.Max(littleEndianScore, bigEndianScore);

            const double strongConfidenceScore = 0.70;
            const double likelyScore = 0.55;
            const double minDirectionGap = 0.08;

            if (bestScore < likelyScore)
            {
                return false;
            }

            if (Math.Abs(littleEndianScore - bigEndianScore) < minDirectionGap && bestScore < strongConfidenceScore)
            {
                return false;
            }

            encoding = littleEndianScore >= bigEndianScore
                ? (System.Text.Encoding)new System.Text.UnicodeEncoding(false, false)
                : new System.Text.UnicodeEncoding(true, false);
            return true;
        }

        private double ScoreUtf16WithoutBom(byte[] buffer, int sampleLength, bool bigEndian)
        {
            int pairCount = sampleLength / 2;
            int nullHighBytes = 0;
            int nullLowBytes = 0;
            int commonTextLowBytes = 0;
            int replacementRisk = 0;

            for (int i = 0; i < sampleLength; i += 2)
            {
                byte high = bigEndian ? buffer[i] : buffer[i + 1];
                byte low = bigEndian ? buffer[i + 1] : buffer[i];

                if (high == 0x00)
                {
                    nullHighBytes++;
                }

                if (low == 0x00)
                {
                    nullLowBytes++;
                }

                if (IsCommonTextByte(low))
                {
                    commonTextLowBytes++;
                }

                if (high >= 0xD8 && high <= 0xDF)
                {
                    replacementRisk++;
                }
            }

            double highNullRatio = (double)nullHighBytes / pairCount;
            double lowNullRatio = (double)nullLowBytes / pairCount;
            double lowTextRatio = (double)commonTextLowBytes / pairCount;
            double replacementRiskRatio = (double)replacementRisk / pairCount;

            double nullLaneBias = Math.Max(0.0, highNullRatio - lowNullRatio);

            return (nullLaneBias * 0.40)
                + (lowTextRatio * 0.35)
                + ((1.0 - replacementRiskRatio) * 0.25);
        }

        private bool IsCommonTextByte(byte value)
        {
            return value == 0x09 || value == 0x0A || value == 0x0D || (value >= 0x20 && value <= 0x7E);
        }

        private bool IsValidUtf8(byte[] buffer, bool allowIncompleteTail)
        {
            int i = 0;
            while (i < buffer.Length)
            {
                byte b = buffer[i];

                if (b <= 0x7F)
                {
                    i++;
                    continue;
                }

                int expectedLength;
                if (b >= 0xC2 && b <= 0xDF)
                {
                    expectedLength = 2;
                }
                else if (b >= 0xE0 && b <= 0xEF)
                {
                    expectedLength = 3;
                }
                else if (b >= 0xF0 && b <= 0xF4)
                {
                    expectedLength = 4;
                }
                else
                {
                    return false;
                }

                if (i + expectedLength > buffer.Length)
                {
                    return allowIncompleteTail;
                }

                if ((buffer[i + 1] & 0xC0) != 0x80)
                {
                    return false;
                }

                if (expectedLength >= 3)
                {
                    if ((buffer[i + 2] & 0xC0) != 0x80)
                    {
                        return false;
                    }

                    if (b == 0xE0 && buffer[i + 1] < 0xA0)
                    {
                        return false;
                    }

                    if (b == 0xED && buffer[i + 1] > 0x9F)
                    {
                        return false;
                    }
                }

                if (expectedLength == 4)
                {
                    if ((buffer[i + 3] & 0xC0) != 0x80)
                    {
                        return false;
                    }

                    if (b == 0xF0 && buffer[i + 1] < 0x90)
                    {
                        return false;
                    }

                    if (b == 0xF4 && buffer[i + 1] > 0x8F)
                    {
                        return false;
                    }
                }

                i += expectedLength;
            }
            return true;
        }

        private bool IsLikelyIso2022Jp(byte[] buffer, bool allowIncompleteTail)
        {
            const byte ESC = 0x1B;
            int escapeSequenceCount = 0;
            bool hasJisMultibyteDesignation = false;

            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] != ESC)
                {
                    continue;
                }

                if (i + 1 >= buffer.Length)
                {
                    return allowIncompleteTail && escapeSequenceCount > 0;
                }

                byte b1 = buffer[i + 1];
                bool isKnownSequence;

                if (b1 == 0x24 || b1 == 0x28)
                {
                    if (i + 2 >= buffer.Length)
                    {
                        return allowIncompleteTail && escapeSequenceCount > 0;
                    }

                    byte b2 = buffer[i + 2];
                    if (b1 == 0x24 && (b2 == 0x40 || b2 == 0x42))
                    {
                        isKnownSequence = true;
                        hasJisMultibyteDesignation = true;
                    }
                    else if (b1 == 0x24 && b2 == 0x28)
                    {
                        if (i + 3 >= buffer.Length)
                        {
                            return allowIncompleteTail && escapeSequenceCount > 0;
                        }

                        // ESC $ ( D (JIS X 0213) も ISO-2022-JP 系列で使用される。
                        isKnownSequence = buffer[i + 3] == 0x44;
                        if (isKnownSequence)
                        {
                            hasJisMultibyteDesignation = true;
                        }
                    }
                    else
                    {
                        isKnownSequence = b1 == 0x28 && (b2 == 0x42 || b2 == 0x4A || b2 == 0x49);
                    }
                }
                else if (b1 == 0x26)
                {
                    if (i + 2 >= buffer.Length)
                    {
                        return allowIncompleteTail && escapeSequenceCount > 0;
                    }

                    if (buffer[i + 2] != 0x40)
                    {
                        return false;
                    }

                    if (i + 3 >= buffer.Length)
                    {
                        return allowIncompleteTail && escapeSequenceCount > 0;
                    }

                    isKnownSequence = buffer[i + 3] == ESC;
                }
                else
                {
                    isKnownSequence = false;
                }

                if (!isKnownSequence)
                {
                    return false;
                }

                escapeSequenceCount++;
            }

            return escapeSequenceCount > 0 && hasJisMultibyteDesignation;
        }

        private bool IsValidEucJp(byte[] buffer, bool allowIncompleteTail)
        {
            int i = 0;
            bool hasMultibyte = false;
            bool hasStrongEucSignature = false;

            while (i < buffer.Length)
            {
                byte b = buffer[i];

                if (b <= 0x7F)
                {
                    i++;
                    continue;
                }

                if (b == 0x8E)
                {
                    if (i + 1 >= buffer.Length)
                    {
                        return allowIncompleteTail && hasMultibyte && hasStrongEucSignature;
                    }

                    byte kana = buffer[i + 1];
                    if (kana < 0xA1 || kana > 0xDF)
                    {
                        return false;
                    }

                    hasMultibyte = true;
                    i += 2;
                    continue;
                }

                if (b == 0x8F)
                {
                    if (i + 2 >= buffer.Length)
                    {
                        return allowIncompleteTail && hasMultibyte && hasStrongEucSignature;
                    }

                    byte b2 = buffer[i + 1];
                    byte b3 = buffer[i + 2];
                    if (b2 < 0xA1 || b2 > 0xFE || b3 < 0xA1 || b3 > 0xFE)
                    {
                        return false;
                    }

                    hasMultibyte = true;
                    hasStrongEucSignature = true;
                    i += 3;
                    continue;
                }

                // EUC-JP 2-byte lead bytes are 0xA1-0xFE.
                if (b >= 0xA1 && b <= 0xFE)
                {
                    if (i + 1 >= buffer.Length)
                    {
                        return allowIncompleteTail && hasMultibyte && hasStrongEucSignature;
                    }

                    byte b2 = buffer[i + 1];
                    if (b2 < 0xA1 || b2 > 0xFE)
                    {
                        return false;
                    }

                    hasMultibyte = true;
                    if (b <= 0xDF)
                    {
                        // CP932 では 0xA1-0xDF は単独の半角カナ領域のため、
                        // この帯域を先頭にした 2 バイト並びは EUC-JP の有力な手掛かりになる。
                        hasStrongEucSignature = true;
                    }
                    i += 2;
                    continue;
                }

                return false;
            }

            if (!hasMultibyte)
            {
                return false;
            }

            if (hasStrongEucSignature)
            {
                return true;
            }

            // 0x8E xx / 0xE0-0xEF 系だけで成立する場合は CP932 と衝突しやすいため、
            // CP932 としても成立する場合は EUC-JP と見なさずフォールバックへ回す。
            return !IsValidCp932(buffer, allowIncompleteTail);
        }

        private bool IsValidCp932(byte[] buffer, bool allowIncompleteTail)
        {
            int i = 0;
            int validLength = buffer.Length;

            while (i < buffer.Length)
            {
                byte b = buffer[i];

                if (b <= 0x7F || (b >= 0xA1 && b <= 0xDF))
                {
                    i++;
                    continue;
                }

                bool isLeadByte = (b >= 0x81 && b <= 0x9F) || (b >= 0xE0 && b <= 0xFC);
                if (!isLeadByte)
                {
                    return false;
                }

                if (i + 1 >= buffer.Length)
                {
                    if (!allowIncompleteTail)
                    {
                        return false;
                    }

                    validLength = i;
                    break;
                }

                byte trail = buffer[i + 1];
                bool isTrailByte = (trail >= 0x40 && trail <= 0x7E) || (trail >= 0x80 && trail <= 0xFC);
                if (!isTrailByte)
                {
                    return false;
                }

                i += 2;
            }

            try
            {
                System.Text.Encoding strictCp932 = System.Text.Encoding.GetEncoding(
                    932,
                    System.Text.EncoderFallback.ExceptionFallback,
                    System.Text.DecoderFallback.ExceptionFallback);
                strictCp932.GetCharCount(buffer, 0, validLength);
                return true;
            }
            catch (System.Text.DecoderFallbackException)
            {
                return false;
            }
        }

        private bool IsLikelyTextContent(byte[] buffer)
        {
            if (buffer.Length == 0)
            {
                return false;
            }

            int sampleLength = Math.Min(buffer.Length, 8192);
            int nullByteCount = 0;
            int controlByteCount = 0;
            int textLikeByteCount = 0;
            int cp932LeadByteCount = 0;
            int cp932KanaByteCount = 0;

            for (int i = 0; i < sampleLength; i++)
            {
                byte b = buffer[i];

                if (b == 0x00)
                {
                    nullByteCount++;
                }

                if ((b <= 0x08) || b == 0x0B || b == 0x0C || (b >= 0x0E && b <= 0x1F) || b == 0x7F)
                {
                    controlByteCount++;
                }

                if (b == 0x09 || b == 0x0A || b == 0x0D || (b >= 0x20 && b <= 0x7E))
                {
                    textLikeByteCount++;
                    continue;
                }

                if (b >= 0xA1 && b <= 0xDF)
                {
                    cp932KanaByteCount++;
                }

                bool isCp932LeadByte = (b >= 0x81 && b <= 0x9F) || (b >= 0xE0 && b <= 0xFC);
                if (isCp932LeadByte)
                {
                    cp932LeadByteCount++;
                }
            }

            if ((double)nullByteCount / sampleLength > 0.01)
            {
                return false;
            }

            if ((double)controlByteCount / sampleLength > 0.02)
            {
                return false;
            }

            double textLikeRatio = (double)textLikeByteCount / sampleLength;
            if (textLikeRatio >= 0.60)
            {
                return true;
            }

            int cp932HintByteCount = cp932LeadByteCount + cp932KanaByteCount;
            return cp932HintByteCount > 0 && (double)(textLikeByteCount + cp932HintByteCount) / sampleLength >= 0.85;
        }

        private bool CheckBom(byte[] buffer)
        {
            if (buffer.Length < 3)
            {
                return false;
            }

            // UTF-8
            if (buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            {
                return true;
            }

            // UTF-16 BE
            if (buffer[0] == 0xFE && buffer[1] == 0xFF)
            {
                return true;
            }

            // UTF-16 LE
            if (buffer[0] == 0xFF && buffer[1] == 0xFE)
            {
                return true;
            }

            if (buffer.Length < 4)
            {
                return false;
            }

            // UTF-32 BE
            if (buffer[0] == 0x00 && buffer[1] == 0x00 && buffer[2] == 0xFE && buffer[3] == 0xFF)
            {
                return true;
            }

            // UTF-32 LE
            if (buffer[0] == 0xFF && buffer[1] == 0xFE && buffer[2] == 0x00 && buffer[3] == 0x00)
            {
                return true;
            }

            return false;
        }

        private System.Text.Encoding ApplyBomSetting(System.Text.Encoding enc, bool flgBom)
        {
            System.Text.Encoding result = enc;

            if (flgBom)
            {
                if (enc is System.Text.UTF8Encoding)
                {
                    result = new System.Text.UTF8Encoding(true);
                }
                else if (enc is System.Text.UnicodeEncoding)
                {
                    if (enc.CodePage == 1200)
                    {
                        result = new System.Text.UnicodeEncoding(false, true);
                    }
                    else if (enc.CodePage == 1201)
                    {
                        result = new System.Text.UnicodeEncoding(true, true);
                    }
                }
                else if (enc is System.Text.UTF32Encoding)
                {
                    if (enc.CodePage == 12000)
                    {
                        result = new System.Text.UTF32Encoding(false, true);
                    }
                    else if (enc.CodePage == 12001)
                    {
                        result = new System.Text.UTF32Encoding(true, true);
                    }
                }
            }
            else
            {
                if (enc is System.Text.UTF8Encoding)
                {
                    result = new System.Text.UTF8Encoding(false);
                }
                else if (enc is System.Text.UnicodeEncoding)
                {
                    if (enc.CodePage == 1200)
                    {
                        result = new System.Text.UnicodeEncoding(false, false);
                    }
                    else if (enc.CodePage == 1201)
                    {
                        result = new System.Text.UnicodeEncoding(true, false);
                    }
                }
                else if (enc is System.Text.UTF32Encoding)
                {
                    if (enc.CodePage == 12000)
                    {
                        result = new System.Text.UTF32Encoding(false, false);
                    }
                    else if (enc.CodePage == 12001)
                    {
                        result = new System.Text.UTF32Encoding(true, false);
                    }
                }
            }

            return result;
        }

        public CsvConfiguration GetCsvConfig()
        {
            if (!this.IsCsvFile)
            {
                return null;
            }

            var config = new CsvConfiguration(System.Globalization.CultureInfo.CurrentCulture)
            {
                Delimiter = this.Delimiter.ToString(),
                HasHeaderRecord = true,
                IgnoreBlankLines = true,
                NewLine = this.NewLine,
                Encoding = this.Encoding,
                ShouldQuote = (context) => HasDoubleQuote,
            };

            return config;
        }

        public List<string> GetColName()
        {
            if (!this.IsCsvFile)
            {
                return new List<string>();
            }

            var config = GetCsvConfig();
            var headerList = new List<string>();
            using (var reader = new StreamReader(this.FilePath, this.Encoding))
            {
                using (var csv = new CsvReader(reader, config))
                {
                    while(csv.Read())
                    {
                        var record = csv.GetRecord<dynamic>() as IDictionary<string,object>;
                        Utils.DebugTool.WriteLine("Raw: " + csv.Context.Parser.RawRecord);
                        foreach(var data in record)
                        {
                            if(!headerList.Contains(data.Key))
                            {
                                Utils.DebugTool.WriteLine("data.Key" + data.Key);
                                Utils.DebugTool.WriteLine("data.Value" + data.Value);
                                headerList.Add(data.Key);
                            }
                        }
                        break;
                    }
                }
            }

            return headerList;
        }
    }
}
