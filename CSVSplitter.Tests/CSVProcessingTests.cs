using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using CSVSplitter.Commands;
using CSVSplitter.Global;
using CSVSplitter.Models;
using CSVSplitter.ViewModels;
using Xunit;

namespace CSVSplitter.Tests
{
    public class CSVProcessingTests
    {
        static CSVProcessingTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [Fact]
        public async Task SortCsvFileAsync_正常にソートできること()
        {
            using var env = new TestEnvironment();
            var input = env.CreateCsv(
                "sort_input.csv",
                "Id,Name,Score",
                new[]
                {
                    "2,Bob,100",
                    "1,Alice,8",
                    "3,Carol,42"
                },
                new UTF8Encoding(false));

            var inputFile = Analyze(input);
            var output = env.Path("sort_output.csv");

            var comparer = new SortComparer(new List<SortOption>
            {
                new SortOption("Id", false, true)
            });

            var command = CreateCommandForPrivateMethods(out var viewModel);
            var count = await InvokeSortCsvFileAsync(command, input, output, inputFile.GetCsvConfig(), comparer, inputFile.RawHeader, 1000);

            Assert.Equal(3, count);
            var lines = ReadAllLines(output, inputFile.Encoding);
            Assert.Equal("Id,Name,Score", lines[0]);
            Assert.Equal("1,Alice,8", lines[1]);
            Assert.Equal("2,Bob,100", lines[2]);
            Assert.Equal("3,Carol,42", lines[3]);
            Assert.True(viewModel.ProgressValue >= 0);
        }

        [Fact]
        public async Task CountCsvFileAsync_ヘッダーを除いたデータ行数を返すこと()
        {
            using var env = new TestEnvironment();
            var input = env.CreateCsv(
                "count_input.csv",
                "Id,Name",
                new[]
                {
                    "1,Alice",
                    "2,Bob",
                    "3,Carol"
                },
                new UTF8Encoding(false));

            var inputFile = Analyze(input);
            var command = CreateCommandForPrivateMethods(out _);
            var count = await InvokeCountCsvFileAsync(command, input, inputFile.GetCsvConfig());

            Assert.Equal(3, count);
        }

        [Fact]
        public async Task EstimateCsvFileRecordsAsync_改行終端とヘッダー有無を考慮して推定できること()
        {
            using var env = new TestEnvironment();
            var withHeader = env.CreateCsv(
                "estimate_with_header.csv",
                "Id,Name",
                new[] { "1,Alice", "2,Bob", "3,Carol" },
                new UTF8Encoding(false));

            var withoutTrailingNewLine = env.Path("estimate_no_trailing_newline.csv");
            File.WriteAllText(withoutTrailingNewLine, "Id,Name\r\n1,Alice\r\n2,Bob", new UTF8Encoding(false));

            var command = CreateCommandForPrivateMethods(out _);
            var withHeaderInput = Analyze(withHeader);
            var withHeaderEstimate = await InvokeEstimateCsvFileRecordsAsync(command, withHeader, withHeaderInput.GetCsvConfig());
            Assert.Equal(3, withHeaderEstimate);

            var configNoHeader = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ",",
                Encoding = new UTF8Encoding(false),
                NewLine = "\r\n",
                HasHeaderRecord = false,
            };
            var noTrailingEstimate = await InvokeEstimateCsvFileRecordsAsync(command, withoutTrailingNewLine, configNoHeader);
            Assert.Equal(3, noTrailingEstimate);
        }

        [Fact]
        public async Task EstimateCsvFileRecordsAsync_空ファイルとヘッダーのみファイルを推定できること()
        {
            using var env = new TestEnvironment();
            var empty = env.Path("empty.csv");
            File.WriteAllText(empty, string.Empty, new UTF8Encoding(false));

            var headerOnly = env.Path("header_only.csv");
            File.WriteAllText(headerOnly, "Id,Name\r\n", new UTF8Encoding(false));

            var command = CreateCommandForPrivateMethods(out _);
            var configWithHeader = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ",",
                Encoding = new UTF8Encoding(false),
                NewLine = "\r\n",
                HasHeaderRecord = true,
            };

            var emptyEstimated = await InvokeEstimateCsvFileRecordsAsync(command, empty, configWithHeader);
            var headerOnlyEstimated = await InvokeEstimateCsvFileRecordsAsync(command, headerOnly, configWithHeader);

            Assert.Equal(0, emptyEstimated);
            Assert.Equal(0, headerOnlyEstimated);
        }

        [Fact]
        public async Task EstimateCsvFileRecordsAsync_引用符内改行がある場合は推定値が実測値以上になりうること()
        {
            using var env = new TestEnvironment();
            var inputPath = env.Path("estimate_with_quoted_newline.csv");
            var content = "Id,Note\r\n1,\"A\r\nB\"\r\n2,\"C\"\r\n";
            File.WriteAllText(inputPath, content, new UTF8Encoding(false));

            var inputFile = Analyze(inputPath);
            var command = CreateCommandForPrivateMethods(out _);
            var estimated = await InvokeEstimateCsvFileRecordsAsync(command, inputPath, inputFile.GetCsvConfig());
            var actual = await InvokeCountCsvFileAsync(command, inputPath, inputFile.GetCsvConfig());

            Assert.Equal(2, actual);
            Assert.True(estimated >= actual);
        }

        [Fact]
        public void ReconcileTotalRecords_推定値から実測値へ一度だけ補正できること()
        {
            var command = CreateCommandForPrivateMethods(out _);
            SetPrivateField(command, "DicEstimatedCsvFile", new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
            {
                ["a.csv"] = 100,
                ["b.csv"] = 150,
            });
            SetPrivateField(command, "ReconciledFiles", new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            var corrected = InvokeReconcileTotalRecords(command, 250, 240, new[] { "a.csv", "b.csv" });
            Assert.Equal(240, corrected);

            var correctedAgain = InvokeReconcileTotalRecords(command, corrected, 240, new[] { "a.csv", "b.csv" });
            Assert.Equal(240, correctedAgain);
        }

        [Fact]
        public void ReconcileTotalRecords_一部ファイルのみ補正した後に残りを補正できること()
        {
            var command = CreateCommandForPrivateMethods(out _);
            SetPrivateField(command, "DicEstimatedCsvFile", new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
            {
                ["a.csv"] = 100,
                ["b.csv"] = 150,
                ["c.csv"] = 200,
            });
            SetPrivateField(command, "ReconciledFiles", new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            var afterA = InvokeReconcileTotalRecords(command, 450, 90, new[] { "a.csv" });
            Assert.Equal(440, afterA); // 450 - 100 + 90

            var afterBC = InvokeReconcileTotalRecords(command, afterA, 320, new[] { "b.csv", "c.csv" });
            Assert.Equal(410, afterBC); // 440 - (150+200) + 320
        }

        [Fact]
        public void UpdateProgressStatus_総量差し替え後も進捗値が範囲内に収まること()
        {
            var vm = new MainWindowViewModel();
            var progress = new UpdateProgressStatus(vm);

            progress.SetTotalAmount(1000);
            progress.SetCount(500);
            progress.SetTotalAmount(800);
            progress.SetCount(700);

            Assert.InRange(vm.ProgressValue, 0, 100);
        }

        [Fact]
        public void SortRows_しきい値未満は同一リストをインプレースソートすること()
        {
            var command = CreateCommandForPrivateMethods(out _);
            var comparer = new SortComparer(new List<SortOption>
            {
                new SortOption("Id", false, true)
            });

            var rows = new List<SortCsvRow>
            {
                CreateRow(("Id", "3")),
                CreateRow(("Id", "1")),
                CreateRow(("Id", "2")),
            };
            rows.ForEach(r => r.SetSortKey(comparer));

            var sorted = InvokeSortRows(command, rows, comparer);

            Assert.Same(rows, sorted);
            Assert.Equal(new[] { "1", "2", "3" }, sorted.Select(r => r.Data["Id"]?.ToString()).ToArray());
        }

        [Fact]
        public void SortRows_しきい値以上は別リストを返してソートすること()
        {
            var command = CreateCommandForPrivateMethods(out _);
            var comparer = new SortComparer(new List<SortOption>
            {
                new SortOption("Id", false, true)
            });
            var threshold = InvokeResolveParallelSortThreshold();
            Assert.True(threshold >= 2);

            var rows = Enumerable.Range(1, threshold)
                .Select(i => CreateRow(("Id", (threshold - i + 1).ToString(CultureInfo.InvariantCulture))))
                .ToList();
            rows.ForEach(r => r.SetSortKey(comparer));

            var sorted = InvokeSortRows(command, rows, comparer);

            Assert.NotSame(rows, sorted);
            Assert.Equal("1", sorted.First().Data["Id"]?.ToString());
            Assert.Equal(threshold.ToString(CultureInfo.InvariantCulture), sorted.Last().Data["Id"]?.ToString());
        }

        [Fact]
        public async Task OutputCsvFileAsync_行数上限で分割されること()
        {
            using var env = new TestEnvironment();
            var input = env.CreateCsv(
                "split_input.csv",
                "Group,Id,Name",
                new[]
                {
                    "A,1,Alice",
                    "A,2,Bob",
                    "A,3,Carol",
                    "A,4,Dave",
                    "A,5,Eve"
                },
                new UTF8Encoding(false));

            var inputFile = Analyze(input);
            var outputBase = env.Path("result.csv");

            var command = CreateCommandForPrivateMethods(out var viewModel);
            viewModel.MaxCsvRecords = 2;
            var splitInfo = new CCSplitInfo();
            splitInfo.AddHeader("Group");

            var count = await InvokeOutputCsvFileAsync(command, input, outputBase, inputFile.GetCsvConfig(), splitInfo, inputFile.RawHeader);
            Assert.Equal(5, count);

            var files = Directory.GetFiles(env.Root, "result_A_*.csv").OrderBy(f => f).ToList();
            Assert.Equal(5, files.Count);

            var recordsPerFile = files.Select(f => ReadAllLines(f, inputFile.Encoding).Length - 1).ToArray();
            Assert.Equal(new[] { 1, 1, 1, 1, 1 }, recordsPerFile);
        }

        [Fact]
        public async Task OutputCsvFileAsync_ダブルクォートを含む値でも分割キーを取得できること()
        {
            using var env = new TestEnvironment();
            var input = env.CreateCsv(
                "quoted_split_input.csv",
                "Group,Name",
                new[]
                {
                    "\"A,1\",Alice",
                    "\"A,1\",Bob",
                    "\"B,2\",Carol"
                },
                new UTF8Encoding(false));

            var inputFile = Analyze(input);
            var outputBase = env.Path("quoted_result.csv");
            var command = CreateCommandForPrivateMethods(out var viewModel);
            viewModel.MaxCsvRecords = 100;
            var splitInfo = new CCSplitInfo();
            splitInfo.AddHeader("Group");

            var count = await InvokeOutputCsvFileAsync(command, input, outputBase, inputFile.GetCsvConfig(), splitInfo, inputFile.RawHeader);
            Assert.Equal(3, count);

            var filesA = Directory.GetFiles(env.Root, "quoted_result_A,1_*.csv").OrderBy(f => f).ToList();
            var filesB = Directory.GetFiles(env.Root, "quoted_result_B,2_*.csv").OrderBy(f => f).ToList();
            Assert.Single(filesA);
            Assert.Single(filesB);
        }

        [Fact]
        public async Task 複数ファイル統合後にソートと分割が正常に動作すること()
        {
            using var env = new TestEnvironment();
            var rows1 = new[]
            {
                "A,3,30",
                "B,4,40"
            };
            var rows2 = new[]
            {
                "A,1,10",
                "A,2,20"
            };

            var file1 = Analyze(env.CreateCsv("part1.csv", "Group,Id,Amount", rows1, new UTF8Encoding(false)));
            var file2 = Analyze(env.CreateCsv("part2.csv", "Group,Id,Amount", rows2, new UTF8Encoding(false)));
            var merged = env.Path("merged.csv");
            var sorted = env.Path("sorted.csv");

            var comparer = new SortComparer(new List<SortOption>
            {
                new SortOption("Group", false, false),
                new SortOption("Id", false, true)
            });

            var command = CreateCommandForPrivateMethods(out var viewModel);

            await command.MergeCsvFileAsync(new List<string> { file1.FilePath, file2.FilePath }, merged, file1.GetCsvConfig(), new SortComparer(new List<SortOption>()), file1.RawHeader);
            await InvokeSortCsvFileAsync(command, merged, sorted, file1.GetCsvConfig(), comparer, file1.RawHeader, 1000);

            viewModel.MaxCsvRecords = 2;
            var splitInfo = new CCSplitInfo();
            splitInfo.AddHeader("Group");
            var outputBase = env.Path("integrated.csv");
            await InvokeOutputCsvFileAsync(command, sorted, outputBase, file1.GetCsvConfig(), splitInfo, file1.RawHeader);

            var groupAFiles = Directory.GetFiles(env.Root, "integrated_A_*.csv").OrderBy(f => f).ToList();
            Assert.Equal(3, groupAFiles.Count);

            var groupARecords = groupAFiles
                .SelectMany(path => ReadAllLines(path, file1.Encoding).Skip(1))
                .ToArray();
            Assert.Equal(new[] { "A,1,10", "A,2,20", "A,3,30" }, groupARecords);
        }

        [Fact]
        public async Task MergeCsvFileAsync_複数入力をキー順にマージできること()
        {
            using var env = new TestEnvironment();
            var part1 = Analyze(env.CreateCsv("merge_part1.csv", "Id,Name", new[] { "1,A", "4,D", "7,G" }, new UTF8Encoding(false)));
            var part2 = Analyze(env.CreateCsv("merge_part2.csv", "Id,Name", new[] { "2,B", "5,E", "8,H" }, new UTF8Encoding(false)));
            var part3 = Analyze(env.CreateCsv("merge_part3.csv", "Id,Name", new[] { "3,C", "6,F", "9,I" }, new UTF8Encoding(false)));

            var output = env.Path("merged_heap.csv");
            var comparer = new SortComparer(new List<SortOption>
            {
                new SortOption("Id", false, true)
            });

            var command = CreateCommandForPrivateMethods(out _);
            var count = await command.MergeCsvFileAsync(
                new List<string> { part1.FilePath, part2.FilePath, part3.FilePath },
                output,
                part1.GetCsvConfig(),
                comparer,
                part1.RawHeader);

            Assert.Equal(9, count);
            var rows = ReadAllLines(output, part1.Encoding).Skip(1).ToArray();
            Assert.Equal(
                new[] { "1,A", "2,B", "3,C", "4,D", "5,E", "6,F", "7,G", "8,H", "9,I" },
                rows);
        }

        [Fact]
        public async Task MergeCsvFileAsync_引用符内改行を含むデータもキー順でマージできること()
        {
            using var env = new TestEnvironment();
            var p1 = env.Path("merge_multiline1.csv");
            var p2 = env.Path("merge_multiline2.csv");
            File.WriteAllText(p1, "Id,Note\r\n1,\"A1\r\nA2\"\r\n3,\"C\"\r\n", new UTF8Encoding(false));
            File.WriteAllText(p2, "Id,Note\r\n2,\"B1\r\nB2\"\r\n4,\"D\"\r\n", new UTF8Encoding(false));

            var f1 = Analyze(p1);
            var f2 = Analyze(p2);
            var output = env.Path("merge_multiline_out.csv");
            var comparer = new SortComparer(new List<SortOption> { new SortOption("Id", false, true) });
            var command = CreateCommandForPrivateMethods(out _);

            var count = await command.MergeCsvFileAsync(new List<string> { f1.FilePath, f2.FilePath }, output, f1.GetCsvConfig(), comparer, f1.RawHeader);
            Assert.Equal(4, count);

            using var reader = new StreamReader(output, f1.Encoding);
            using var csv = new CsvReader(reader, f1.GetCsvConfig());
            var ids = new List<string>();
            var notes = new List<string>();
            if (await csv.ReadAsync())
            {
                csv.ReadHeader();
            }
            while (await csv.ReadAsync())
            {
                ids.Add(csv.GetField("Id"));
                notes.Add(csv.GetField("Note"));
            }
            Assert.Equal(new[] { "1", "2", "3", "4" }, ids);
            Assert.Equal("A1\r\nA2", notes[0]);
            Assert.Equal("B1\r\nB2", notes[1]);
        }

        [Fact]
        public async Task SortCsvFileAsync_最大レコード超過時でもソートできること()
        {
            using var env = new TestEnvironment();
            const int headerCount = 201;
            var headers = Enumerable.Range(1, headerCount).Select(i => "C" + i).ToArray();
            var header = string.Join(",", headers);

            int rows = 2050;
            var data = new List<string>(rows);
            for (int i = rows; i >= 1; i--)
            {
                // C1 は逆順の数値、他列は固定
                data.Add(i.ToString(CultureInfo.InvariantCulture) + new string(',', headerCount - 1));
            }

            var path = env.CreateCsv("large.csv", header, data, new UTF8Encoding(false));
            var inputFile = Analyze(path);
            var output = env.Path("large_sorted.csv");

            var maxSort = Parameter.GetMaxSortFileRecords(headerCount);
            Assert.True(maxSort <= rows);

            var comparer = new SortComparer(new List<SortOption>
            {
                new SortOption("C1", false, true)
            });

            var command = CreateCommandForPrivateMethods(out _);
            var count = await InvokeSortCsvFileAsync(command, path, output, inputFile.GetCsvConfig(), comparer, inputFile.RawHeader, maxSort);

            Assert.Equal(rows, count);
            var lines = ReadAllLines(output, inputFile.Encoding);
            Assert.Equal("1" + new string(',', headerCount - 1), lines[1]);
            Assert.Equal(rows.ToString(CultureInfo.InvariantCulture) + new string(',', headerCount - 1), lines[^1]);
        }

        [Fact]
        public void SortComparer_昇順降順数値文字列の組み合わせでソートできること()
        {
            var rows = new List<SortCsvRow>
            {
                CreateRow(("Name", "Alice"), ("Score", "2"), ("Category", "B")),
                CreateRow(("Name", "alice"), ("Score", "10"), ("Category", "A")),
                CreateRow(("Name", "Bob"), ("Score", "1"), ("Category", "A")),
            };

            var options = new List<SortOption>
            {
                new SortOption("Category", false, false),
                new SortOption("Score", true, true),
                new SortOption("Name", false, false),
            };
            var comparer = new SortComparer(options);
            rows.ForEach(r => r.SetSortKey(comparer));

            rows = rows.OrderBy(r => r, comparer).ToList();

            Assert.Equal("alice", rows[0].Data["Name"]);
            Assert.Equal("Bob", rows[1].Data["Name"]);
            Assert.Equal("Alice", rows[2].Data["Name"]);
        }

        [Fact]
        public async Task SortCsvFileAsync_存在しないソート列を指定しても例外にならないこと()
        {
            using var env = new TestEnvironment();
            var input = env.CreateCsv(
                "missing_sort_column.csv",
                "Id,Name",
                new[]
                {
                    "2,Bob",
                    "1,Alice"
                },
                new UTF8Encoding(false));

            var inputFile = Analyze(input);
            var output = env.Path("missing_sort_column_out.csv");
            var comparer = new SortComparer(new List<SortOption>
            {
                new SortOption("NotFoundColumn", false, false),
                new SortOption("Id", false, true),
            });

            var command = CreateCommandForPrivateMethods(out _);
            var count = await InvokeSortCsvFileAsync(command, input, output, inputFile.GetCsvConfig(), comparer, inputFile.RawHeader, 1000);
            Assert.Equal(2, count);
        }

        [Theory]
        [MemberData(nameof(GetEncodingPatternCases))]
        public void 文字コード判定_多様な文字コードと文字種パターンでCSVとして認識できること(
            string name,
            string encodingName,
            bool withBom,
            bool forceEncodingWithoutBom,
            string contentKind,
            int? expectedCodePage)
        {
            using var env = new TestEnvironment();
            var encoding = Encoding.GetEncoding(encodingName);
            var content = CreateCsvContentForEncodingTest(contentKind, "\r\n");

            var file = env.Path(name + "_" + contentKind + ".csv");
            WriteCsv(file, content, encoding, withBom, forceEncodingWithoutBom);

            var inputFile = new InputFile { FilePath = file };
            inputFile.Analyze();

            Assert.True(inputFile.IsAnalyzed);
            Assert.True(inputFile.IsCsvFile);
            Assert.True(inputFile.IsTextFile);
            Assert.Equal(",", inputFile.Delimiter.ToString());
            Assert.Equal("\r\n", inputFile.NewLine);
            Assert.NotEmpty(inputFile.Header);
            if (expectedCodePage.HasValue)
            {
                Assert.Equal(expectedCodePage.Value, inputFile.Encoding.CodePage);
            }
        }

        [Fact]
        public void 文字コード判定_小容量と大容量のUTF8ファイルを判定できること()
        {
            using var env = new TestEnvironment();
            var small = env.CreateCsv("small.csv", "A,B", new[] { "1,あ" }, new UTF8Encoding(false));

            var largeRows = Enumerable.Range(1, 30000)
                .Select(i => $"{i},カタカナ{i % 10},ｶﾅ{i % 5}")
                .ToArray();
            var large = env.CreateCsv("large_utf8.csv", "Id,Name,HalfKana", largeRows, new UTF8Encoding(false));

            var smallFile = Analyze(small);
            var largeFile = Analyze(large);

            Assert.Equal(65001, smallFile.Encoding.CodePage);
            Assert.Equal(65001, largeFile.Encoding.CodePage);
            Assert.True(smallFile.IsCsvFile);
            Assert.True(largeFile.IsCsvFile);
        }

        [Fact]
        public async Task SortCsvFileAsync_ヘッダーと改行コードを維持できること()
        {
            using var env = new TestEnvironment();
            var inputPath = env.Path("lf.csv");
            var content = CreateCsvContent("Id,Name", new[] { "2,B", "1,A" }, "\n");
            WriteCsv(inputPath, content, new UTF8Encoding(false), false, false);

            var inputFile = Analyze(inputPath);
            Assert.Equal("\n", inputFile.NewLine);

            var output = env.Path("lf_sorted.csv");
            var comparer = new SortComparer(new List<SortOption> { new SortOption("Id", false, true) });
            var command = CreateCommandForPrivateMethods(out _);
            await InvokeSortCsvFileAsync(command, inputPath, output, inputFile.GetCsvConfig(), comparer, inputFile.RawHeader, 1000);

            var raw = File.ReadAllText(output, inputFile.Encoding);
            Assert.StartsWith("Id,Name\n", raw, StringComparison.Ordinal);
            Assert.DoesNotContain("\r\n", raw, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SortCsvFileAsync_クォートを含むフィールドを処理できること()
        {
            using var env = new TestEnvironment();
            var input = env.CreateCsv(
                "quoted.csv",
                "\"Id\",\"Name\",\"Note\"",
                new[]
                {
                    "\"2\",\"Bob\",\"x,y\"",
                    "\"1\",\"Alice\",\"\"\"quote\"\"\""
                },
                new UTF8Encoding(false));

            var inputFile = Analyze(input);
            Assert.True(inputFile.HasDoubleQuote);

            var output = env.Path("quoted_sorted.csv");
            var comparer = new SortComparer(new List<SortOption> { new SortOption("Id", false, true) });
            var command = CreateCommandForPrivateMethods(out _);

            await InvokeSortCsvFileAsync(command, input, output, inputFile.GetCsvConfig(), comparer, inputFile.RawHeader, 1000);
            var lines = ReadAllLines(output, inputFile.Encoding);
            Assert.Equal("\"1\",\"Alice\",\"\"\"quote\"\"\"", lines[1]);
            Assert.Equal("\"2\",\"Bob\",\"x,y\"", lines[2]);
        }

        [Fact]
        public async Task SortCsvFileAsync_引用符内改行とカンマを含むセルを保持できること()
        {
            using var env = new TestEnvironment();
            var inputPath = env.Path("quoted_multiline.csv");
            var content = "Id,Note\r\n2,\"line1\r\nline2,with comma\"\r\n1,\"single line\"\r\n";
            File.WriteAllText(inputPath, content, new UTF8Encoding(false));

            var inputFile = Analyze(inputPath);
            var output = env.Path("quoted_multiline_sorted.csv");
            var comparer = new SortComparer(new List<SortOption> { new SortOption("Id", false, true) });
            var command = CreateCommandForPrivateMethods(out _);
            var count = await InvokeSortCsvFileAsync(command, inputPath, output, inputFile.GetCsvConfig(), comparer, inputFile.RawHeader, 1000);
            Assert.Equal(2, count);

            using var reader = new StreamReader(output, inputFile.Encoding);
            using var csv = new CsvReader(reader, inputFile.GetCsvConfig());
            if (await csv.ReadAsync())
            {
                csv.ReadHeader();
            }
            var rows = new List<Dictionary<string, string>>();
            while (await csv.ReadAsync())
            {
                rows.Add(new Dictionary<string, string>
                {
                    ["Id"] = csv.GetField("Id"),
                    ["Note"] = csv.GetField("Note")
                });
            }

            Assert.Equal("1", rows[0]["Id"]);
            Assert.Equal("single line", rows[0]["Note"]);
            Assert.Equal("2", rows[1]["Id"]);
            Assert.Equal("line1\r\nline2,with comma", rows[1]["Note"]);
        }

        [Fact]
        public async Task OutputCsvFileAsync_複数分割キーで正しく分割できること()
        {
            using var env = new TestEnvironment();
            var input = env.CreateCsv(
                "multi_split.csv",
                "Group,Type,Id",
                new[]
                {
                    "A,X,1",
                    "A,Y,2",
                    "A,X,3",
                    "B,X,4"
                },
                new UTF8Encoding(false));

            var inputFile = Analyze(input);
            var command = CreateCommandForPrivateMethods(out var viewModel);
            viewModel.MaxCsvRecords = 100;

            var splitInfo = new CCSplitInfo();
            splitInfo.AddHeader("Group");
            splitInfo.AddHeader("Type");

            await InvokeOutputCsvFileAsync(command, input, env.Path("out.csv"), inputFile.GetCsvConfig(), splitInfo, inputFile.RawHeader);

            var files = Directory.GetFiles(env.Root, "out_*_*.csv").Select(System.IO.Path.GetFileName).OrderBy(x => x).ToArray();
            Assert.Equal(new[] { "out_A_X_1.csv", "out_A_Y_1.csv", "out_B_X_1.csv" }, files);
        }

        [Fact]
        public async Task OutputCsvFileAsync_引用符とエスケープを含むキーで分割できること()
        {
            using var env = new TestEnvironment();
            var inputPath = env.Path("quoted_escape_split.csv");
            var content =
                "Group,Type,Id\r\n" +
                "\"A,1\",\"X\"\"Y\",1\r\n" +
                "\"A,1\",\"X\"\"Y\",2\r\n" +
                "\"B,2\",\"Q\"\"R\",3\r\n";
            File.WriteAllText(inputPath, content, new UTF8Encoding(false));

            var inputFile = Analyze(inputPath);
            var outputBase = env.Path("quoted_escape_result.csv");
            var command = CreateCommandForPrivateMethods(out var viewModel);
            viewModel.MaxCsvRecords = 100;
            var splitInfo = new CCSplitInfo();
            splitInfo.AddHeader("Group");
            splitInfo.AddHeader("Type");

            var count = await InvokeOutputCsvFileAsync(command, inputPath, outputBase, inputFile.GetCsvConfig(), splitInfo, inputFile.RawHeader);
            Assert.Equal(3, count);

            var filesA = Directory.GetFiles(env.Root, "quoted_escape_result_A,1_X*Y_*.csv");
            var filesB = Directory.GetFiles(env.Root, "quoted_escape_result_B,2_Q*R_*.csv");
            Assert.Single(filesA);
            Assert.Single(filesB);
        }

        [Fact]
        public async Task OutputCsvFileAsync_分割キー列が存在しない場合は空キーとして処理できること()
        {
            using var env = new TestEnvironment();
            var input = env.CreateCsv(
                "missing_split_header.csv",
                "Group,Id",
                new[]
                {
                    "A,1",
                    "B,2"
                },
                new UTF8Encoding(false));

            var inputFile = Analyze(input);
            var outputBase = env.Path("missing_split_result.csv");
            var command = CreateCommandForPrivateMethods(out var viewModel);
            viewModel.MaxCsvRecords = 100;
            var splitInfo = new CCSplitInfo();
            splitInfo.AddHeader("NotFound");

            var count = await InvokeOutputCsvFileAsync(command, input, outputBase, inputFile.GetCsvConfig(), splitInfo, inputFile.RawHeader);
            Assert.Equal(2, count);

            var files = Directory.GetFiles(env.Root, "missing_split_result_*.csv");
            Assert.Single(files);
        }

        [Fact]
        public void GetOutputFilePath_無効文字と空白を正規化できること()
        {
            var command = CreateCommandForPrivateMethods(out _);
            var used = new List<string>();
            var outputFolder = System.IO.Path.GetTempPath();

            var file1 = command.GetOutputFilePath("a:b c　d", ".csv", outputFolder, ref used);
            used.Add(file1);
            var file2 = command.GetOutputFilePath("a:b c　d", ".csv", outputFolder, ref used);

            Assert.DoesNotContain(":", System.IO.Path.GetFileName(file1), StringComparison.Ordinal);
            Assert.DoesNotContain(" ", System.IO.Path.GetFileName(file1), StringComparison.Ordinal);
            Assert.NotEqual(file1, file2);
            Assert.EndsWith("_2.csv", file2, StringComparison.Ordinal);
        }

        [Fact]
        public void InputFile_存在しないファイルは未解析扱いになること()
        {
            var inputFile = new InputFile { FilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".csv") };
            inputFile.Analyze();
            Assert.False(inputFile.IsAnalyzed);
            Assert.False(inputFile.IsCsvFile);
        }

        [Fact]
        public void InputFile_CSVでないテキストをCSV扱いしないこと()
        {
            using var env = new TestEnvironment();
            var path = env.Path("not_csv.txt");
            File.WriteAllText(path, "just text without delimiter", new UTF8Encoding(false));

            var inputFile = new InputFile { FilePath = path };
            inputFile.Analyze();
            Assert.True(inputFile.IsAnalyzed);
            Assert.False(inputFile.IsCsvFile);
        }

        [Fact]
        public void InputFile_混在バイナリデータをCSV扱いしないこと()
        {
            using var env = new TestEnvironment();
            var path = env.Path("binary.csv");
            File.WriteAllBytes(path, new byte[] { 0x00, 0xFF, 0x81, 0x00, 0x02, 0x03, 0xFE });

            var inputFile = new InputFile { FilePath = path };
            inputFile.Analyze();
            Assert.True(inputFile.IsAnalyzed);
            Assert.False(inputFile.IsCsvFile);
        }

        [Fact]
        public void InputFiles_ヘッダーまたは文字コード不一致の場合統合不可になること()
        {
            using var env = new TestEnvironment();
            var files = new InputFiles();
            files.Add(env.CreateCsv("u8.csv", "A,B", new[] { "あ,2" }, new UTF8Encoding(false)));
            files.Add(env.CreateCsv("sjis.csv", "A,B", new[] { "①,2" }, Encoding.GetEncoding(932)));
            files.Analyze();

            Assert.Equal(65001, files[0].Encoding.CodePage);
            Assert.Equal(932, files[1].Encoding.CodePage);
            Assert.False(files.HasUniformHeaders);
        }

        [Fact]
        public void ConvertCommand_CanExecute_前提条件で有効無効が切り替わること()
        {
            using var env = new TestEnvironment();
            var vm = new MainWindowViewModel();
            var cmd = new ConvertCommand(vm);

            Assert.False(cmd.CanExecute(null));

            vm.AddInputFile(env.CreateCsv("a.csv", "A,B", new[] { "1,2" }, new UTF8Encoding(false)));
            vm.AnalyzeInputFiles();
            Assert.True(cmd.CanExecute(null));

            vm.ChangeProcessingStatus(true);
            Assert.False(cmd.CanExecute(null));
        }

        [Fact]
        public async Task MergeCsvFileAsync_昇順データを維持してマージできること()
        {
            using var env = new TestEnvironment();
            var file1 = Analyze(env.CreateCsv("m1.csv", "Id,Name", new[] { "1,A", "3,C" }, new UTF8Encoding(false)));
            var file2 = Analyze(env.CreateCsv("m2.csv", "Id,Name", new[] { "2,B", "4,D" }, new UTF8Encoding(false)));
            var output = env.Path("merged_sorted.csv");
            var comp = new SortComparer(new List<SortOption> { new SortOption("Id", false, true) });

            var command = CreateCommandForPrivateMethods(out _);
            var count = await command.MergeCsvFileAsync(new List<string> { file1.FilePath, file2.FilePath }, output, file1.GetCsvConfig(), comp, file1.RawHeader);

            Assert.Equal(4, count);
            var lines = ReadAllLines(output, file1.Encoding);
            Assert.Equal(new[] { "Id,Name", "1,A", "2,B", "3,C", "4,D" }, lines);
        }

        [Fact]
        public async Task SortComparer_ランダムデータでも単調順序を満たすこと()
        {
            using var env = new TestEnvironment();
            var random = new Random(42);
            var rows = new List<string>();
            for (int i = 0; i < 500; i++)
            {
                rows.Add($"{random.Next(0, 1000)},{(char)('A' + random.Next(0, 5))}");
            }

            var input = env.CreateCsv("rand.csv", "Score,Group", rows, new UTF8Encoding(false));
            var inputFile = Analyze(input);
            var output = env.Path("rand_sorted.csv");
            var comparer = new SortComparer(new List<SortOption>
            {
                new SortOption("Score", false, true),
                new SortOption("Group", true, false),
            });

            var command = CreateCommandForPrivateMethods(out _);
            await InvokeSortCsvFileAsync(command, input, output, inputFile.GetCsvConfig(), comparer, inputFile.RawHeader, 1000);

            using var reader = new StreamReader(output, inputFile.Encoding);
            using var csv = new CsvReader(reader, inputFile.GetCsvConfig());
            SortCsvRow previous = null;
            if (await csv.ReadAsync())
            {
                csv.ReadHeader();
            }
            while (await csv.ReadAsync())
            {
                var header = csv.HeaderRecord ?? Array.Empty<string>();
                var record = csv.Parser.Record ?? Array.Empty<string>();
                var data = new Dictionary<string, string>(header.Length, StringComparer.Ordinal);
                for (int i = 0; i < header.Length; i++)
                {
                    data[header[i]] = i < record.Length ? record[i] : null;
                }
                var now = new SortCsvRow
                {
                    Data = data
                };
                now.SetSortKey(comparer);
                if (previous != null)
                {
                    Assert.True(comparer.Compare(previous, now) <= 0);
                }
                previous = now;
            }
        }

        private static SortCsvRow CreateRow(params (string Key, string Value)[] pairs)
        {
            var row = new SortCsvRow
            {
                Data = pairs.ToDictionary(p => p.Key, p => p.Value)
            };
            return row;
        }

        private static ConvertCommand CreateCommandForPrivateMethods(out MainWindowViewModel viewModel)
        {
            viewModel = new MainWindowViewModel();
            var command = new ConvertCommand(viewModel);

            SetPrivateField(command, "DicCountCsvFile", new Dictionary<string, long>());
            SetPrivateField(command, "outputFiles", new List<string>());
            SetPrivateField(command, "updateProgressStatus", new UpdateProgressStatus(viewModel));
            return command;
        }

        private static async Task<long> InvokeSortCsvFileAsync(ConvertCommand command, string inputFile, string outputFile, CsvConfiguration config, SortComparer comp, string rawHeader, long maxSortFileRecords)
        {
            var method = typeof(ConvertCommand).GetMethod("SortCsvFileAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            var task = (Task<long>)method.Invoke(command, new object[] { inputFile, outputFile, config, comp, rawHeader, maxSortFileRecords });
            return await task;
        }

        private static async Task<long> InvokeOutputCsvFileAsync(ConvertCommand command, string inputFile, string outputFile, CsvConfiguration config, CCSplitInfo splitInfo, string rawHeader)
        {
            var method = typeof(ConvertCommand).GetMethod("OutputCsvFileAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            var task = (Task<long>)method.Invoke(command, new object[] { inputFile, outputFile, config, splitInfo, rawHeader });
            return await task;
        }

        private static async Task<long> InvokeCountCsvFileAsync(ConvertCommand command, string inputFile, CsvConfiguration config)
        {
            var method = typeof(ConvertCommand).GetMethod("CountCsvFileAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            var task = (Task<long>)method.Invoke(command, new object[] { inputFile, config });
            return await task;
        }

        private static async Task<long> InvokeEstimateCsvFileRecordsAsync(ConvertCommand command, string inputFile, CsvConfiguration config)
        {
            var method = typeof(ConvertCommand).GetMethod("EstimateCsvFileRecordsAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            var task = (Task<long>)method.Invoke(command, new object[] { inputFile, config });
            return await task;
        }

        private static long InvokeReconcileTotalRecords(ConvertCommand command, long estimatedTotalRecords, long actualRecords, IEnumerable<string> files)
        {
            var method = typeof(ConvertCommand).GetMethod("ReconcileTotalRecords", BindingFlags.NonPublic | BindingFlags.Instance);
            return (long)method.Invoke(command, new object[] { estimatedTotalRecords, actualRecords, files });
        }

        private static List<SortCsvRow> InvokeSortRows(ConvertCommand command, List<SortCsvRow> rows, SortComparer comparer)
        {
            var method = typeof(ConvertCommand).GetMethod("SortRows", BindingFlags.NonPublic | BindingFlags.Instance);
            return (List<SortCsvRow>)method.Invoke(command, new object[] { rows, comparer });
        }

        private static int InvokeResolveParallelSortThreshold()
        {
            var method = typeof(ConvertCommand).GetMethod(
                "ResolveParallelSortThreshold",
                BindingFlags.NonPublic | BindingFlags.Static);

            if (method == null)
            {
                throw new MissingMethodException(typeof(ConvertCommand).FullName, "ResolveParallelSortThreshold");
            }

            return (int)method.Invoke(null, null);
        }

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }

            var property = instance.GetType().GetProperty(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(instance, value);
                return;
            }

            throw new InvalidOperationException($"Private member '{fieldName}' was not found on {instance.GetType().FullName}.");
        }

        private static InputFile Analyze(string filePath)
        {
            var inputFile = new InputFile { FilePath = filePath };
            inputFile.Analyze();
            Assert.True(inputFile.IsCsvFile);
            return inputFile;
        }

        private static string[] ReadAllLines(string filePath, Encoding encoding)
        {
            return File.ReadAllLines(filePath, encoding).Where(x => !string.IsNullOrEmpty(x)).ToArray();
        }

        private static string CreateCsvContent(string header, IEnumerable<string> rows, string newLine)
        {
            var sb = new StringBuilder();
            sb.Append(header).Append(newLine);
            foreach (var row in rows)
            {
                sb.Append(row).Append(newLine);
            }
            return sb.ToString();
        }

        public static IEnumerable<object[]> GetEncodingPatternCases()
        {
            // ASCII のみデータは UTF-8 / Shift-JIS / EUC-JP が同一バイト列になり判別困難なため除外する。
            var baseCases = new[]
            {
                new { Name = "utf8_nobom", EncodingName = "utf-8", WithBom = false, ForceWithoutBom = false, ExpectedCodePage = (int?)65001, Kinds = new[] { "alnum", "japanese", "fullwidth_kana", "halfwidth_kana", "mixed_all" } },
                new { Name = "utf8_bom", EncodingName = "utf-8", WithBom = true, ForceWithoutBom = false, ExpectedCodePage = (int?)65001, Kinds = new[] { "alnum", "japanese", "fullwidth_kana", "halfwidth_kana", "mixed_all" } },
                new { Name = "utf16le_bom", EncodingName = "utf-16", WithBom = true, ForceWithoutBom = false, ExpectedCodePage = (int?)1200, Kinds = new[] { "alnum", "japanese", "fullwidth_kana", "halfwidth_kana", "mixed_all" } },
                new { Name = "utf16be_bom", EncodingName = "utf-16BE", WithBom = true, ForceWithoutBom = false, ExpectedCodePage = (int?)1201, Kinds = new[] { "alnum", "japanese", "fullwidth_kana", "halfwidth_kana", "mixed_all" } },
                new { Name = "utf16le_nobom", EncodingName = "utf-16", WithBom = false, ForceWithoutBom = true, ExpectedCodePage = (int?)1200, Kinds = new[] { "alnum", "japanese", "fullwidth_kana", "halfwidth_kana", "mixed_all" } },
                new { Name = "utf16be_nobom", EncodingName = "utf-16BE", WithBom = false, ForceWithoutBom = true, ExpectedCodePage = (int?)1201, Kinds = new[] { "alnum", "japanese", "fullwidth_kana", "halfwidth_kana", "mixed_all" } },
                new { Name = "shift_jis", EncodingName = "shift_jis", WithBom = false, ForceWithoutBom = false, ExpectedCodePage = (int?)932, Kinds = new[] { "japanese", "fullwidth_kana", "halfwidth_kana", "mixed_all" } },
                new { Name = "euc_jp", EncodingName = "euc-jp", WithBom = false, ForceWithoutBom = false, ExpectedCodePage = (int?)51932, Kinds = new[] { "japanese", "fullwidth_kana" } },
            };

            foreach (var baseCase in baseCases)
            {
                foreach (var kind in baseCase.Kinds)
                {
                    yield return new object[]
                    {
                        baseCase.Name,
                        baseCase.EncodingName,
                        baseCase.WithBom,
                        baseCase.ForceWithoutBom,
                        kind,
                        baseCase.ExpectedCodePage
                    };
                }
            }
        }

        private static string CreateCsvContentForEncodingTest(string contentKind, string newLine)
        {
            switch (contentKind)
            {
                case "alnum":
                    return CreateCsvContent(
                        "Type,Text,Value",
                        new[]
                        {
                            "ALNUM,abcXYZ123,100",
                            "ALNUM,mix987QWE,200"
                        },
                        newLine);
                case "japanese":
                    return CreateCsvContent(
                        "種別,本文,値",
                        new[]
                        {
                            "日本語,東京大阪京都,100",
                            "日本語,日本語だけ,200"
                        },
                        newLine);
                case "fullwidth_kana":
                    return CreateCsvContent(
                        "種別,カナ,値",
                        new[]
                        {
                            "全角,アイウエオ,100",
                            "全角,カキクケコ,200"
                        },
                        newLine);
                case "halfwidth_kana":
                    return CreateCsvContent(
                        "種別,半角ｶﾅ,値",
                        new[]
                        {
                            "半角,ｱｲｳｴｵ,100",
                            "半角,ｶｷｸｹｺ,200"
                        },
                        newLine);
                case "mixed_all":
                    return CreateCsvContent(
                        "種別,本文,値",
                        new[]
                        {
                            "混在,ABC123日本語アイウｱｲｳ,100",
                            "混在,Z9東京カキクｶｷｸ,200"
                        },
                        newLine);
                default:
                    throw new ArgumentOutOfRangeException(nameof(contentKind), contentKind, "Unknown encoding test content kind.");
            }
        }

        private static void WriteCsv(string filePath, string content, Encoding encoding, bool withBom, bool forceEncodingWithoutBom)
        {
            Encoding payloadEncoding;
            if (forceEncodingWithoutBom && (encoding.CodePage == 1200 || encoding.CodePage == 1201))
            {
                if (encoding.CodePage == 1200)
                {
                    payloadEncoding = new UnicodeEncoding(false, false);
                }
                else
                {
                    payloadEncoding = new UnicodeEncoding(true, false);
                }
            }
            else if (encoding.CodePage == 65001)
            {
                payloadEncoding = new UTF8Encoding(false);
            }
            else if (encoding.CodePage == 1200)
            {
                payloadEncoding = new UnicodeEncoding(false, false);
            }
            else if (encoding.CodePage == 1201)
            {
                payloadEncoding = new UnicodeEncoding(true, false);
            }
            else
            {
                payloadEncoding = encoding;
            }

            var payloadBytes = payloadEncoding.GetBytes(content);
            if (withBom && !forceEncodingWithoutBom)
            {
                var preamble = encoding.GetPreamble();
                if (preamble.Length > 0)
                {
                    payloadBytes = preamble.Concat(payloadBytes).ToArray();
                }
            }

            File.WriteAllBytes(filePath, payloadBytes);
        }

        private sealed class TestEnvironment : IDisposable
        {
            public string Root { get; }

            public TestEnvironment()
            {
                Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CSVSplitterTests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Root);
            }

            public string Path(string fileName)
            {
                return System.IO.Path.Combine(Root, fileName);
            }

            public string CreateCsv(string fileName, string header, IEnumerable<string> rows, Encoding encoding)
            {
                var path = Path(fileName);
                using var writer = new StreamWriter(path, false, encoding);
                writer.WriteLine(header);
                foreach (var row in rows)
                {
                    writer.WriteLine(row);
                }
                return path;
            }

            public void Dispose()
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, true);
                }
            }
        }
    }
}
