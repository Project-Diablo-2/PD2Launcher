﻿
namespace PD2Shared.Models
{
    public class AllSettings
    {
        public DdrawOptions DdrawOptions { get; set; }
        public FileUpdateModel FileUpdateModel { get; set; }
        public SelectedAuthorAndFilter SelectedAuthorAndFilter { get; set; }
        public Pd2AuthorList Pd2AuthorList { get; set; }
        public LauncherArgs LauncherArgs { get; set; }
        public News News { get; set; }
        public WindowPositionModel WindowPosition { get; set; }
        public ResetInfo ResetInfo { get; set; }
    }
}