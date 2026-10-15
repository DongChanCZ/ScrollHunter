mergeInto(LibraryManager.library, {
  ScrollHunter_GetLaunchLanguage: function () {
    var value = window.scrollHunterLaunchLanguage;
    return typeof value === 'number' && value % 1 === 0 && value >= 0 && value <= 4 ? value : -1;
  },
  ScrollHunter_RememberLanguage: function (value) {
    if (typeof window.scrollHunterRememberLanguage === 'function')
      window.scrollHunterRememberLanguage(value);
  }
});
