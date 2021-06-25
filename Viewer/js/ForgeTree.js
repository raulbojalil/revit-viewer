$(document).ready(function () {

  const urlParams = new URLSearchParams(window.location.search);
  const urn = urlParams.get('urn');

  launchViewer(urn);
});