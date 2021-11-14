"use strict";

function showLoader(flag) {
    if (flag === true) {
        $("#loader").show();
        $("#loaderMask").show();
    }
    else {
        $("#loaderMask").hide();
        $("#loader").hide();
    }
}