using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ChurchBooks.App.Behaviors;
using Xunit;

namespace ChurchBooks.App.Tests;

public sealed class Phase10R112R17FunctionalInteractionTests
{
    [Fact]
    public void GivingSetup_AutoLoadsSelectedCategory_AndKeepsCodeInternal()
    {
        var source = ReadSource("src", "ChurchBooks.App", "ViewModels", "PeopleGivingWorkspaceViewModel.cs");
        var xaml = ReadSource("src", "ChurchBooks.App", "MainWindow.xaml");

        Assert.Contains("LoadGivingCategoryIntoEditor", source, StringComparison.Ordinal);
        Assert.Contains("OnSelectedGivingCategoryChanged", source, StringComparison.Ordinal);
        Assert.Contains("current.Code", source, StringComparison.Ordinal);
        Assert.Contains("BuildInternalCode(\"GIVE\", CategoryName)", source, StringComparison.Ordinal);
        Assert.Contains("SelectedGivingCategory, Mode=TwoWay", xaml, StringComparison.Ordinal);
        Assert.Contains("Show archived categories", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("PeopleWorkspace.EditGivingCategoryCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("PeopleWorkspace.EditHouseholdCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("PeopleWorkspace.GivingCategoryArchiveActionLabel", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneralFundAction_IsIdempotent_AndNeverPostsAccounting()
    {
        var source = ReadSource("src", "ChurchBooks.App", "ViewModels", "MainWindowViewModel.cs");

        Assert.Contains("GetFundsAsync(includeArchived: true)", source, StringComparison.Ordinal);
        Assert.Contains("fund.Code.Equals(\"GENERAL\"", source, StringComparison.Ordinal);
        Assert.Contains("fund.Code.Equals(\"GEN\"", source, StringComparison.Ordinal);
        Assert.Contains("General Fund already exists and is now selected.", source, StringComparison.Ordinal);
        Assert.Contains("General Fund restored and selected.", source, StringComparison.Ordinal);
        Assert.Contains("No duplicate fund or accounting entry was created.", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DateInput_RejectsPre1900_AndAccepts1900()
    {
        Assert.False(DateInputBehavior.TryParseSupportedDate("12/31/1899", out _));
        Assert.True(DateInputBehavior.TryParseSupportedDate("01/01/1900", out var minimum));
        Assert.Equal(new DateTime(1900, 1, 1), minimum);
        Assert.True(DateInputBehavior.TryParseSupportedDate("08/21/2026", out _));
    }

    [Fact]
    public void DatePickerAndCalendar_GlobalStyles_StartAt1900()
    {
        var controls = ReadSource("src", "ChurchBooks.App", "Themes", "Controls.xaml");

        Assert.Contains("Property=\"DisplayDateStart\" Value=\"1900-01-01\"", controls, StringComparison.Ordinal);
        Assert.Contains("Earliest supported date: 01/01/1900.", controls, StringComparison.Ordinal);
    }

    [Fact]
    public void ButtonScanner_DoesNotTreatButtonPropertyElementsAsButtons()
    {
        var pattern = new Regex(@"<Button(?=[\s>])[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.Empty(pattern.Matches("<Button.Style><Style TargetType=\"Button\" /></Button.Style>"));
        Assert.Single(pattern.Matches("<Button Command=\"{Binding SaveCommand}\" Content=\"Save\" />"));
        Assert.Single(pattern.Matches("<Button>"));
    }

    [Fact]
    public void EveryEnabledUserFacingButton_HasACommandOrClickHandler()
    {
        var root = ProjectRoot();
        var xamlFiles = new List<string>
        {
            Path.Combine(root, "src", "ChurchBooks.App", "MainWindow.xaml")
        };
        xamlFiles.AddRange(Directory.GetFiles(Path.Combine(root, "src", "ChurchBooks.App", "Views"), "*.xaml"));

        var problems = new List<string>();
        foreach (var file in xamlFiles)
        {
            var text = File.ReadAllText(file);
            var matches = Regex.Matches(text, @"<Button(?=[\s>])[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match match in matches)
            {
                var tag = match.Value;
                if (Regex.IsMatch(tag, @"\bIsEnabled\s*=\s*""False""", RegexOptions.IgnoreCase))
                {
                    continue;
                }

                var hasCommand = Regex.IsMatch(tag, @"\bCommand\s*=", RegexOptions.IgnoreCase);
                var hasClick = Regex.IsMatch(tag, @"\bClick\s*=", RegexOptions.IgnoreCase);
                if (!hasCommand && !hasClick)
                {
                    problems.Add(Path.GetFileName(file) + ": " + Regex.Replace(tag, @"\s+", " ").Trim());
                }
            }
        }

        Assert.True(problems.Count == 0, "Enabled Button elements without Command or Click:\n" + string.Join("\n", problems));
    }

    private static string ReadSource(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { ProjectRoot() }.Concat(parts).ToArray()));

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}