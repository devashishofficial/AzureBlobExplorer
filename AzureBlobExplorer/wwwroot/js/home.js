"use strict";

$(function () {
    showLoader(false);
    listContainers($("#accounts a")[0]);
});

function listContainers(ele) {
    $("#access").val($(ele).data("permission-type"));
    $.ajax({
        url: '/Home/ListContainers?sASName=' + ele.id,
        type: 'POST',
        dataType: 'json',
        success: function success(containers) {
            $(".nav.nav-underline").empty();
            for (var i = 0; i < containers.length; i++) {
                $(".nav.nav-underline").append("<a class='nav-link active' href='#'>" + containers[i].name + "</a>");
            }
            listBlobs('');
        },
        error: onError
    });
}

function listBlobs(prefix) {
    prefix = decodeURIComponent(prefix);
    $.ajax({
        url: '/Home/ListBlobs',
        type: 'POST',
        dataType: 'json',
        data: {
            "prefix": prefix
        },
        beforeSend: function beforeSend() {
            showLoader(true);
        },
        complete: function complete() {
            showLoader(false);
        },
        success: function success(blobs) {
            $("#dataTable tbody").empty();
            var tempFolder = "";

            for (var i = 0; i < blobs.length; i++) {
                var listBlob = blobs[i].uri.split('/');

                if (blobs[i].isDirectory) {
                    if (prefix === undefined) prefix = "";
                    tempFolder = prefix + (prefix !== "" ? "/" + listBlob[listBlob.length - 2] : listBlob[listBlob.length - 2]);
                    $("#dataTable tbody").append("<tr><td><i class=\"fas fa-folder\" title=\"Folder\"></i></td><td></td><td></td><td class=\"h4\"><a href=\"#\" class=\"badge badge-light\" onclick='listBlobs(\""
                        .concat(tempFolder, "\")'>").concat(decodeURIComponent(listBlob[listBlob.length - 2]), "</a></td><td></td><td></td><td></td></tr>"));
                }
                else {
                    var fullFileName = prefix !== "" ? prefix + "/" + listBlob[listBlob.length - 1] : listBlob[listBlob.length - 1];
                    var deleteHtml = "<button type=\"button\" class=\"btn btn-link\"><i class=\"fas fa-trash\" title=\"Delete\" onclick='deleteBolb(\"".concat(fullFileName, "\")'></i></button>");
                    var downloadHtml = "<button type=\"button\" class=\"btn btn-link\"><i class=\"fas fa-download\" title=\"Download\" onclick='downloadBlob(\"".concat(fullFileName, "\", \"").concat(listBlob[listBlob.length - 1], "\")'></i></button>");

                    $("#dataTable tbody").append("<tr><td><i class=\"fas fa-file\" title=\"File\"></i></td><td>"
                        .concat($('#access').val() >= 4 ? deleteHtml : '', "</td><td>")
                        .concat($('#access').val() >= 2 ? downloadHtml : '', "</td><td class=\"h5\">")
                        .concat(decodeURIComponent(listBlob[listBlob.length - 1]), "</td><td>")
                        .concat(formatBytes(blobs[i].size), "</td><td>")
                        .concat(moment(blobs[i].created).format('MMMM Do YYYY, h:mm:ss a'), "</td><td>")
                        .concat(moment(blobs[i].lastModified).format('MMMM Do YYYY, h:mm:ss a'), "</td></tr>"));
                }
            }

            $("#blobCount").text(blobs.length);

            if (prefix === undefined || prefix === "") {
                $("#backButton").attr("disabled", true);
            } else {
                $("#backButton").removeAttr("disabled");
            }

            $("#folder").val(prefix);
            $("#folderDisplay").text(prefix);
        },
        error: onError
    });
}

function backFolder() {
    var folder = $("#folder").val();
    var str = "/?" + folder.substr(folder.lastIndexOf('/') + 1) + '$';
    listBlobs(folder.replace(new RegExp(str), ''));
}

function deleteBolb(uri) {
    bootbox.confirm('Are you sure to delete this blob?', function (result) {
        if (result) $.ajax({
            url: "/Home/DeleteBlob?blobUri=" + uri,
            type: 'GET',
            beforeSend: function beforeSend() {
                showLoader(true);
            },
            complete: function complete() {
                showLoader(false);
            },
            success: function success() {
                listBlobs($("#folder").val());
            },
            error: onError
        });
    });
}

function openUploadModal() {
    $("#blobInput").val('');
    $("#folderName").val('');
    $("#overWriteWarning").hide();
    $("#spinner").hide();
}

function uploadBlob() {
    var files = $("#blobInput")[0].files;
    if (files.length === 0) return;

    var formData = new FormData();

    for (var i = 0; i < files.length; i++) {
        if (files[i].size > 2147483648000) {
            bootbox.alert("Invalid file size.");
            return;
        }
        var fileName = $("#folderName").val() !== "" ? $("#folderName").val() + "/" + files[i].name : files[i].name;
        var fullFileName = $("#folder").val() !== "" ? $("#folder").val() + "/" + fileName : fileName;
        formData.append('files', files[i], fullFileName);
        $("#fileNameUp").text(fullFileName);
    }
    $.ajax({
        url: '/Home/ExistsBlob',
        type: 'POST',
        dataType: 'json',
        data: { "blobUri": fullFileName },
        success: function success(data) {
            if (data) {
                bootbox.confirm("The file already exists. Do you want to overwrite it?", function (result) {
                    if (result) uploadBlobAjax(formData, fullFileName);
                });
            }
            else {
                uploadBlobAjax(formData, fullFileName);
            }
        },
        error: onError
    });
}

function uploadBlobAjax(formData, fullFileName) {
    $.ajax({
        url: "/Home/UploadBlob",
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        beforeSend: function beforeSend() {
            $('#fileUploadModal').modal('hide');
            $("#upToast").clone().attr("id", "upToast" + fullFileName).appendTo(".toastContainer");
            $(document.getElementById("upToast" + fullFileName)).toast("show");
            $(document.getElementById("upToast" + fullFileName)).find(".nameField").html(fullFileName);
            startProgressBar(fullFileName);
        },
        complete: function complete() {
            stopProgressBar(fullFileName);
            $(document.getElementById("upToast" + fullFileName)).toast("dispose");
            $(document.getElementById("upToast" + fullFileName)).remove();
        },
        xhr: function xhr() {
            var xhr = $.ajaxSettings.xhr();

            xhr.upload.onprogress = function (data) {
                var progress = Math.round(data.loaded / data.total * 100);
                $(document.getElementById("upToast" + fullFileName)).find(".up .bar").css({
                    width: progress + "%"
                });
                $(document.getElementById("upToast" + fullFileName)).find(".up .label").html(progress + "%");
            };

            return xhr;
        },
        success: function success(data) {
            console.log(data);
            listBlobs($("#folder").val());
            $("#blobInput").val('');
        },
        error: onError
    });
}

var intervalId = {};

function startProgressBar(fileId) {
    intervalIdDown[fileId] = setInterval(function () {
        $.ajax({
            url: "/Home/Progress?isUp=true&fileId=" + fileId,
            type: 'POST',
            dataType: 'json',
            success: function success(progress) {
                $(document.getElementById("upToast" + fileId)).find(".process .bar").css({
                    width: progress + "%"
                });
                $(document.getElementById("upToast" + fileId)).find(".process .label").html(progress + "%");

                if (progress === 100) {
                    stopProgressBarDown(fileId);
                }
            },
            error: function error(jqXHR, textStatus, errorMessage) {
                stopProgressBar(fileId);
                onError(jqXHR, textStatus, errorMessage);
            }
        });
    }, 5000);
}

function stopProgressBar(file) {
    clearInterval(intervalId[file]);
}

function downloadBlob(uri, fileName) {
    $("#fileNameDown").text(fileName);
    $.ajax({
        url: "/Home/DownloadBlob?blobUri=" + uri,
        method: 'GET',
        xhrFields: {
            responseType: 'blob'
        },
        beforeSend: function beforeSend() {
            $("#downToast").clone().attr("id", "downToast" + fileName).appendTo(".toastContainer");
            $(document.getElementById("downToast" + fileName)).toast("show");
            $(document.getElementById("downToast" + fileName)).find(".nameField").html(fileName);
            startProgressBarDown(fileName);
        },
        xhr: function xhr() {
            var xhr = $.ajaxSettings.xhr();

            xhr.onprogress = function (data) {
                var progress = Math.round(data.loaded / data.total * 100);
                $(document.getElementById("downToast" + fileName)).find(".down .bar").css({
                    width: progress + "%"
                });
                $(document.getElementById("downToast" + fileName)).find(".down .label").html(progress + "%");
            };

            return xhr;
        },
        complete: function complete() {
            stopProgressBarDown(fileName);
            $(document.getElementById("downToast" + fileName)).toast("dispose");
            $(document.getElementById("downToast" + fileName)).remove();
        },
        success: function success(data, textStatus, jqXHR) {
            if (jqXHR.getResponseHeader('Content-Disposition').indexOf("||Error|") >= 0) {
                var sIndex = jqXHR.getResponseHeader('Content-Disposition').indexOf("||Error|");
                var eIndex = jqXHR.getResponseHeader('Content-Disposition').indexOf("|Error||");
                bootbox.alert(jqXHR.getResponseHeader('Content-Disposition').substring(sIndex + 8, eIndex));
            }
            else {
                var a = document.createElement('a');
                var url = window.URL.createObjectURL(data);
                a.href = url;
                var listUri = uri.split('/');
                a.download = listUri[listUri.length - 1];
                document.body.append(a);
                a.click();
                a.remove();
                window.URL.revokeObjectURL(url);
            }
        },
        error: onError
    });
}

var intervalIdDown = {};

function startProgressBarDown(fileId) {
    intervalIdDown[fileId] = setInterval(function () {
        $.ajax({
            url: "/Home/Progress?isUp=false&fileId=" + fileId,
            type: 'POST',
            dataType: 'json',
            success: function success(progress) {
                $(document.getElementById("downToast" + fileId)).find(".process .bar").css({
                    width: progress + "%"
                });
                $(document.getElementById("downToast" + fileId)).find(".process .label").html(progress + "%");

                if (progress === 100) {
                    stopProgressBarDown(fileId);
                }
            },
            error: function error(jqXHR, textStatus, errorMessage) {
                stopProgressBarDown(fileId);
                onError(jqXHR, textStatus, errorMessage);
            }
        });
    }, 5000);
}

function stopProgressBarDown(file) {
    clearInterval(intervalIdDown[file]);
}

function onError(jqXHR, textStatus, errorMessage) {
    //console.log(errorMessage);
    bootbox.alert(jqXHR.responseText === "" || jqXHR.responseText === undefined ? errorMessage : jqXHR.responseText);
}

function formatBytes(bytes, decimals = 2) {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const dm = decimals < 0 ? 0 : decimals;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
}