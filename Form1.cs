using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace KobaltBuilder
{
    public partial class Form1 : Form
    {
        private ProgressBarManager.ProgressBarManager progressBarManager;
        private ArchiveExtractor _extractor;
        private Dictionary<string, string> filesDictionary = new Dictionary<string, string>();
        private string _filePath = "";
        private List<string> _originalListNPKItems = new List<string>();

        public Form1()
        {
            InitializeComponent();
            progressBarManager = new ProgressBarManager.ProgressBarManager(progressBar1);
            filesDictionary = new Dictionary<string, string>();
            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;
            comboBox1.Items.AddRange(new string[] { "", ".wav", ".ntx", ".n", ".nvx", ".nax", ".txt" });
            checkBox2.CheckedChanged += checkBox2_CheckedChanged;
            checkBox3.CheckedChanged += checkBox3_CheckedChanged;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Archive files (*.npk)|*.npk|All files (*.*)|*.*"
            };
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                textBox1.Text = Path.GetFileName(filePath);
                LoadArchive(filePath);
            }
        }

        private void LoadArchive(string filePath)
        {
            _extractor = new ArchiveExtractor(filePath);
            _extractor.ProgressChanged += OnProgressChanged;
            try
            {
                progressBarManager.SetProgress(progressBar1.Value);
                _extractor.Extract();
                UpdateFileList();
                DisplayArchiveInfo(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
            finally
            {
                _extractor.ProgressChanged -= OnProgressChanged;
            }
        }

        private void UpdateFileList()
        {
            listBox1.Items.Clear();
            foreach (var entry in _extractor.GetFileNames())
            {
                listBox1.Items.Add(entry);
            }
        }

        private void DisplayArchiveInfo(string filePath)
        {
            toolStripLabel2.Text = $"{(new FileInfo(filePath).Length / 1024)} KB";
            toolStripLabel4.Text = $"{_extractor.GetFileNames().Count} files";
        }


        private void OnProgressChanged(int progress)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<int>(OnProgressChanged), progress);
            }
            else
            {
                progressBar1.Value = progress;
                if (progress == 100)
                {
                    MessageBox.Show("Archive loaded successfully.");
                    progressBar1.Value = 0;
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex != -1 && _extractor != null)
            {
                try
                {
                    _extractor.ExtractFile(listBox1.SelectedIndex, "ExtractedFile", progressBar1);
                    MessageBox.Show("File extracted successfully.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("Please select a file to extract.");
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (_extractor != null)
            {
                string outputDirectory = textBox3.Text.Trim();
                if (string.IsNullOrWhiteSpace(outputDirectory))
                {
                    MessageBox.Show("Please enter a valid output directory.");
                    return;
                }
                try
                {
                    _extractor.ExtractAllFiles(outputDirectory, progressBar1);
                    MessageBox.Show("All files extracted successfully.");
                    progressBar1.Value = 0;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("Please open an archive first.");
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex != -1 && _extractor != null)
            {
                var selectedFileIndex = listBox1.SelectedIndex;
                var selectedFileName = _extractor.GetFileNames()[selectedFileIndex];

                foreach (var entry in _extractor.GetDirectoryEntries())
                {
                    if (entry.EntryType == "ELIF" && entry.FileName == selectedFileName)
                    {
                        toolStripLabel6.Text = $"{entry.FileOffset} bytes";
                        toolStripLabel8.Text = $"{(entry.FileLength / 1024)} KB";
                        break;
                    }
                }
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_extractor != null && listBox1.Items.Count > 0)
            {
                string selectedFormat = comboBox1.SelectedItem?.ToString();
                if (!string.IsNullOrEmpty(selectedFormat))
                {
                    listBox1.Items.Clear();

                    foreach (var entry in _extractor.GetFileNames())
                    {
                        if (Path.GetExtension(entry).Equals(selectedFormat, StringComparison.OrdinalIgnoreCase))
                        {
                            listBox1.Items.Add(entry);
                        }
                    }
                }
                else
                {
                    listBox1.Items.Clear();
                    foreach (var entry in _extractor.GetFileNames())
                    {
                        listBox1.Items.Add(entry);
                    }
                }
                if (checkBox2.Checked)
                {
                    NumberListBoxItems();
                }
            }
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            Application.Restart();
        }

        public class ArchiveExtractor
        {
            private readonly string _filePath;
            private ArchiveHeader _archiveHeader;
            private List<DirectoryEntry> _directoryEntries;
            private List<FileData> _fileDataList;
            public event Action<int> ProgressChanged;

            public ArchiveExtractor(string filePath)
            {
                _filePath = filePath;
                _directoryEntries = new List<DirectoryEntry>();
                _fileDataList = new List<FileData>();
            }

            public void Extract()
            {
                if (!File.Exists(_filePath))
                {
                    throw new FileNotFoundException($"The file {_filePath} does not exist.");
                }

                using (BinaryReader reader = new BinaryReader(File.Open(_filePath, FileMode.Open), Encoding.UTF8))
                {
                    ReadArchiveHeader(reader);
                    ReadDetailsDirectory(reader);
                    ReadFileData(reader);
                }
            }

            private void ReadArchiveHeader(BinaryReader reader)
            {
                _archiveHeader = new ArchiveHeader
                {
                    Header = reader.ReadUInt32(),
                    Version = reader.ReadUInt32(),
                    FileDataOffset = reader.ReadUInt32()
                };
            }

            private void ReadDetailsDirectory(BinaryReader reader)
            {
                string currentDirectory = string.Empty;

                while (reader.BaseStream.Position < _archiveHeader.FileDataOffset)
                {
                    DirectoryEntry entry = new DirectoryEntry
                    {
                        EntryType = Encoding.UTF8.GetString(reader.ReadBytes(4)),
                        EntryLength = reader.ReadUInt32()
                    };

                    if (entry.EntryType == "_RID")
                    {
                        ushort dirNameLength = reader.ReadUInt16();
                        entry.DirectoryName = Encoding.UTF8.GetString(reader.ReadBytes(dirNameLength));
                        currentDirectory = entry.DirectoryName;
                    }
                    else if (entry.EntryType == "ELIF")
                    {
                        entry.FileOffset = reader.ReadUInt32();
                        entry.FileLength = reader.ReadUInt32();
                        ushort fileNameLength = reader.ReadUInt16();
                        entry.FileName = Encoding.UTF8.GetString(reader.ReadBytes(fileNameLength));
                        entry.FullPath = Path.Combine(currentDirectory, entry.FileName);

                        if (_directoryEntries == null)                                                                                  // Initialize TOC, only if doees not exist yet.
                        {
                            _directoryEntries = new List<DirectoryEntry>();
                        }
                        _directoryEntries.Add(entry);                                                                                   // Add elem to toc
                    }
                    else
                    {
                        reader.BaseStream.Position += entry.EntryLength;
                    }
                }
            }

            private void ReadFileData(BinaryReader reader)
            {
                int fileCount = _directoryEntries.Count;
                int processedCount = 0;

                foreach (var entry in _directoryEntries)
                {
                    if (entry.EntryType == "ELIF")
                    {
                        reader.BaseStream.Seek(_archiveHeader.FileDataOffset + entry.FileOffset, SeekOrigin.Begin);
                        byte[] data = reader.ReadBytes((int)entry.FileLength);
                        FileData fileData = new FileData
                        {
                            FileName = entry.FileName,
                            FullPath = entry.FullPath,
                            Data = data,
                            DataLength = entry.FileLength
                        };
                        _fileDataList.Add(fileData);

                        processedCount++;
                        ProgressChanged?.Invoke(processedCount * 100 / fileCount);
                    }
                }
            }

            public List<string> GetFileNames()
            {
                return _directoryEntries.FindAll(e => e.EntryType == "ELIF").ConvertAll(e => e.FileName);
            }

            public List<DirectoryEntry> GetDirectoryEntries()
            {
                return _directoryEntries;
            }

            public void ExtractFile(int fileIndex, string outputDirectory, ProgressBar progressBar)
            {
                if (fileIndex < 0 || fileIndex >= _fileDataList.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(fileIndex), "Invalid file index.");
                }
                var fileData = _fileDataList[fileIndex];
                ExtractFile(fileData, outputDirectory, progressBar);
            }

            private void ExtractFile(FileData fileData, string outputDirectory, ProgressBar progressBar)
            {
                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }
                string outputPath = Path.Combine(outputDirectory, fileData.FullPath);
                string directoryPath = Path.GetDirectoryName(outputPath);

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                File.WriteAllBytes(outputPath, fileData.Data);
                progressBar.Value++;
            }

            public void ExtractAllFiles(string outputDirectory, ProgressBar progressBar)
            {
                if (string.IsNullOrWhiteSpace(outputDirectory))
                {
                    MessageBox.Show("Please specify a valid output directory");
                    return;
                }

                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }
                progressBar.Maximum = _fileDataList.Count;
                progressBar.Value = 0;

                foreach (var fileData in _fileDataList)
                {
                    ExtractFile(fileData, outputDirectory, progressBar);
                }
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            string configFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dat/cfg");
            string configFilePath = Path.Combine(configFolderPath, "config.cfg");

            if (listBox2.Items.Count == 0)
            {
                MessageBox.Show("Please add files to the archive first.");
                return;
            }

            if (string.IsNullOrEmpty(textBox4.Text))
            {
                MessageBox.Show("Please enter an archive name.");
                return;
            }

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Archive files (*.npk)|*.npk";
                saveFileDialog.FileName = textBox4.Text.Trim();

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string archivePath = saveFileDialog.FileName;

                    try
                    {
                        using (FileStream archiveStream = new FileStream(archivePath, FileMode.Create))
                        using (BinaryWriter writer = new BinaryWriter(archiveStream, Encoding.UTF8))
                        {
                            writer.Write(Encoding.UTF8.GetBytes("0KPN"));                                                               // Write archive header
                            writer.Write((uint)4);                                                                                      // Version
                            long fileDataOffsetPosition = writer.BaseStream.Position;
                            writer.Write((uint)0);                                                                                      // Placeholder for FileDataOffset

                            uint fileDataOffset = (uint)(12 + listBox2.Items.Count * (4 + 4 + 2));                                      // Initial file data offset estimation

                            progressBar2.Minimum = 0;
                            progressBar2.Maximum = listBox2.Items.Count;
                            progressBar2.Value = 0;

                            List<(string fileName, byte[] fileData)> files = new List<(string, byte[])>();

                            foreach (var fileName in listBox2.Items)
                            {
                                if (filesDictionary.TryGetValue(fileName.ToString(), out string fullPath))
                                {
                                    byte[] fileNameBytes = Encoding.UTF8.GetBytes(fileName.ToString());
                                    byte[] fileData = File.ReadAllBytes(fullPath);

                                    files.Add((fileName.ToString(), fileData));

                                    writer.Write(Encoding.UTF8.GetBytes("ELIF"));                                                       // Write entry type (ELIF)
                                    uint entryLength = (uint)(4 + 4 + 2 + fileNameBytes.Length);                                        // Calculate and write entry length
                                    writer.Write(entryLength);
                                    writer.Write(fileDataOffset);                                                                       // Write file offset and length
                                    writer.Write((uint)fileData.Length);
                                    writer.Write((ushort)fileNameBytes.Length);                                                         // Write filename length and filename
                                    writer.Write(fileNameBytes);

                                    fileDataOffset += (uint)fileData.Length;

                                    progressBar2.Value++;
                                }
                                else
                                {
                                    throw new FileNotFoundException($"File not found in dictionary: {fileName}");
                                }
                            }

                            long currentPosition = writer.BaseStream.Position;
                            writer.Seek((int)fileDataOffsetPosition, SeekOrigin.Begin);
                            writer.Write((uint)currentPosition);
                            writer.Seek((int)currentPosition, SeekOrigin.Begin);

                            foreach (var file in files)
                            {
                                writer.Write(file.fileData);                             // Write file data
                            }
                        }

                        MessageBox.Show("Archive created successfully.");
                        progressBar2.Value = 0;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred while creating the archive: {ex.Message}");
                    }
                }
            }
        }

        private void button9_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Multiselect = true;
                openFileDialog.Filter = "All files (*.*)|*.*";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    progressBar2.Minimum = 0;
                    progressBar2.Maximum = openFileDialog.FileNames.Length;
                    progressBar2.Value = 0;
                    foreach (string filePath in openFileDialog.FileNames)
                    {
                        string fileName = Path.GetFileName(filePath);
                        if (!filesDictionary.ContainsKey(fileName))
                        {
                            filesDictionary.Add(fileName, filePath);
                            listBox2.Items.Add(fileName);
                        }
                        else
                        {
                            MessageBox.Show($"File with the name '{fileName}' already exists in the list.");
                        }

                        progressBar2.Value++;
                    }
                    progressBar2.Value = 0;
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dat/cfg", "config.cfg");

            if (File.Exists(configFilePath))
            {
                try
                {
                    using (StreamReader reader = new StreamReader(configFilePath))
                    {
                        while (!reader.EndOfStream)
                        {
                            string line = reader.ReadLine();

                            if (line.StartsWith("TextBox3="))
                            {
                                textBox3.Text = line.Substring("TextBox3=".Length);
                            }
                            else if (line.StartsWith("TextBox2="))
                            {
                                textBox2.Text = line.Substring("TextBox2=".Length);
                            }
                            else if (line.StartsWith("CheckBox1="))
                            {
                                checkBox1.Checked = bool.Parse(line.Substring("CheckBox1(".Length));
                            }

                            else if (line.StartsWith("CheckBox2="))
                            {
                                checkBox2.Checked = bool.Parse(line.Substring("CheckBox2=".Length));
                            }

                            else if (line.StartsWith("CheckBox3="))
                            {
                                checkBox3.Checked = bool.Parse(line.Substring("CheckBox3=".Length));
                            }

                            else if (line.StartsWith("TextBox5="))
                            {
                                textBox4.Text = line.Substring("TextBox5=".Length);
                            }
                        }
                    }

                    ApplyCheckBox2Setting();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while loading the configuration: {ex.Message}");
                }
            }
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            ApplyCheckBox2Setting();
        }

        private void ApplyCheckBox2Setting()
        {
            UnnumberListBoxItems();

            if (checkBox2.Checked)
            {
                NumberListBoxItems();
            }
        }

        private void NumberListBoxItems()
        {
            List<string> items = new List<string>();
            for (int i = 0; i < listBox1.Items.Count; i++)
            {
                items.Add($"{i + 1}. {listBox1.Items[i]}");
            }
            listBox1.Items.Clear();
            listBox1.Items.AddRange(items.ToArray());
        }

        private void UnnumberListBoxItems()
        {
            List<string> items = new List<string>();
            for (int i = 0; i < listBox1.Items.Count; i++)
            {
                string item = listBox1.Items[i].ToString();
                items.Add(item.Substring(item.IndexOf(' ') + 1));
            }
            listBox1.Items.Clear();
            listBox1.Items.AddRange(items.ToArray());
        }

        private void button6_Click(object sender, EventArgs e)
        {
            string configFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dat/cfg");
            string configFilePath = Path.Combine(configFolderPath, "config.cfg");

            // Ensure the directory exists
            if (!Directory.Exists(configFolderPath))
            {
                Directory.CreateDirectory(configFolderPath);
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(configFilePath))
                {
                    writer.WriteLine($"TextBox3={textBox3.Text}");
                    writer.WriteLine($"TextBox2={textBox2.Text}");
                    writer.WriteLine($"CheckBox2={checkBox2.Checked}");
                    writer.WriteLine($"CheckBox3={checkBox3.Checked}");
                    writer.WriteLine($"TextBox5 ={textBox5.Text}");
                }

                MessageBox.Show("Configuration saved successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while saving the configuration: {ex.Message}");
            }
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            ApplyCheckBox3Setting();
        }

        private void ApplyCheckBox3Setting()
        {
            if (checkBox3.Checked)
            {
                SortListNPKItems();
            }
            else
            {
                UnsortListNPKItems();
            }
        }

        private void SortListNPKItems()
        {
            List<string> items = listBox1.Items.Cast<string>().ToList();
            items.Sort();
            listBox1.Items.Clear();
            listBox1.Items.AddRange(items.ToArray());
        }

        private void UnsortListNPKItems()
        {
            if (_originalListNPKItems != null)
            {
                listBox1.Items.Clear();
                listBox1.Items.AddRange(_originalListNPKItems.ToArray());
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            listBox1.Refresh();
            listBox1.Update();
        }
    }
}