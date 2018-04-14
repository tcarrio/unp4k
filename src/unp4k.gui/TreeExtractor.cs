﻿using ICSharpCode.SharpZipLib.Zip;
using Ookii.Dialogs.Wpf;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Path = System.IO.Path;
using unp4k.gui.Extensions;
using unp4k.gui.TreeModel;
using unp4k.gui.Plugins;

namespace unp4k.gui
{
	public class TreeExtractor
	{
		private ZipFile _pak;
		public Predicate<Object> Filter { get; }

		public TreeExtractor(ZipFile pak, Predicate<Object> filter)
		{
			this._pak = pak;
			this.Filter = filter;
		}

		public async Task ExtractNodeAsync(ITreeItem selectedItem, Boolean useTemp = false)
		{
			if (selectedItem == null) return;

			Boolean? result = false;
			String path = String.Empty;

			if (useTemp)
			{
				path = Path.Combine(Path.GetTempPath(), "unp4k", selectedItem.Title);
				result = true;
			}

			if (String.IsNullOrWhiteSpace(path))
			{
				if (selectedItem is IStreamTreeItem)
				{
					var dlg = new VistaSaveFileDialog
					{
						FileName = selectedItem.Title,
						OverwritePrompt = true,
						Title = $"Export {selectedItem.Title} File",
						Filter = $"Selected File|{Path.GetExtension(selectedItem.Title)}",
					};

					result = dlg.ShowDialog();
					path = dlg.FileName;
				}

				else if (selectedItem is IBranchItem)
				{
					var dlg = new VistaFolderBrowserDialog
					{
						Description = $"Export {selectedItem.Title} Directory",
						UseDescriptionForTitle = true,
						SelectedPath = selectedItem.Title,
					};

					result = dlg.ShowDialog();
					path = dlg.SelectedPath;
				}
			}

			if (result == true)
			{
				await this.ExtractNodeAsync(selectedItem, path);

				if (useTemp) System.Diagnostics.Process.Start(path);
			}

			await Task.CompletedTask;
		}

		private async Task ExtractNodeAsync(ITreeItem node, String outputRoot, String rootPath = null)
		{
			// Early exit if we don't match the filter
			if (!this.Filter(node)) return;

			if (node is IStreamTreeItem leaf)
			{
				await this.ExtractNodeAsync(leaf, outputRoot, rootPath);
			}

			if (node is IBranchItem branch)
			{
				await this.ExtractNodeAsync(branch, outputRoot, rootPath);
			}
			
			// else
			// {
			// 	throw new NotSupportedException($"Node type not supported. Node type: {node.GetType().Name}");
			// }
		}

		private async Task ExtractNodeAsync(IStreamTreeItem node, String outputRoot, String rootPath)
		{
			var forgeFactory = new DataForgeFormatFactory { };
			var cryxmlFactory = new CryXmlFormatFactory { };

			node = forgeFactory.Extract(node);
			node = cryxmlFactory.Extract(node);

			if (rootPath == null)
			{
				rootPath = Path.GetDirectoryName(node.RelativePath);
				outputRoot = Path.GetDirectoryName(outputRoot);
			}

			// Get file path relative to the passed root
			var relativePath = node.RelativePath.RelativeTo(rootPath);
			var absolutePath = Path.Combine(outputRoot, relativePath);

			if (!String.IsNullOrWhiteSpace(absolutePath))
			{
				var target = new FileInfo(absolutePath);

				if (!target.Directory.Exists) target.Directory.Create();

				#region Dump Raw File

				using (var dataStream = node.Stream)
				{
					dataStream.Seek(0, SeekOrigin.Begin);

					using (FileStream fs = File.Create(absolutePath))
					{
						await dataStream.CopyToAsync(fs, 4096);
					}
				}

				#endregion
			}
		}

		private async Task ExtractNodeAsync(IBranchItem node, String outputRoot, String rootPath)
		{
			if (rootPath == null)
			{
				rootPath = String.Empty;

				if (!String.IsNullOrWhiteSpace(node.RelativePath))
				{
					rootPath = Path.GetDirectoryName(node.RelativePath);
				}
			}
			
			foreach (var child in node.Children)
			{
				await this.ExtractNodeAsync(child, outputRoot, rootPath);
			}
		}
	}
}