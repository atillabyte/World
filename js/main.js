var doc = document.documentElement;

var canvasfg = document.getElementById("canvasfg");
var canvasbg = document.getElementById("canvasbg");
var ctxfg = canvasfg.getContext("2d");
var ctxbg = canvasbg.getContext("2d");

var width = 25, height = 25;
var worlddata = [];
var name = "";
var blocks = [];

$(window).waitForImages(function() {
    $(document).ready(function() {
    adjustCanvas();

    clearMap();

    for(i = 0; i < 15; i++)
        addBlock(0, 1+i, 1, i);
    });
});

doc.ondragover = function() { return false; };
doc.ondragend = function() { return false; };
doc.ondrop = function(event) {
    event.preventDefault && event.preventDefault();
    var reader = new FileReader();
    reader.onloadend = function() {
        init_json(JSON.parse(this.result));
    };
    reader.readAsText(event.dataTransfer.files[0]);
    return false;
}

function adjustCanvas() {
    ctxfg.canvas.width = width*16;
    ctxfg.canvas.height = height*16;
    ctxbg.canvas.width = width*16;
    ctxbg.canvas.height = height*16;
}

function addBlock(layer, x, y, type, rotation) {
    if (rotation == null)
        rotation = -1;

    var tag = "b" + type + (((rotation > 1) ? "_"+rotation : ""));
    var find = blocks.find(x => x.tag == tag);
    var bitmap;

    if (find != null)
        bitmap = find.bitmap;
    else {
        bitmap = new Image();

        var element = document.getElementById(tag);

        if (element != null) {
            bitmap.src = element.src;
            
            if (bitmap.complete && (typeof bitmap.naturalWidth != "undefined" && bitmap.naturalWidth != 0)) {
                blocks.push({ tag: tag, bitmap: bitmap });
            } else {
                return;
            }
        } else return;
    }
    
    if (layer == 0)
        ctxfg.drawImage(bitmap, 0, 0, 16, 16, x*16, y*16, 16, 16);
    else if (layer == 1)
        ctxbg.drawImage(bitmap, 0, 0, 16, 16, x*16, y*16, 16, 16);
}

function clearMap() {
    ctxfg.clearRect(0, 0, canvasfg.width, canvasfg.height);
    ctxbg.clearRect(0, 0, canvasbg.width, canvasbg.height);

    for (var x = 0; x < width; x++)
        for (var y = 0; y < height; y++)
            if (x == 0 || y == 0 || x == width-1 || y == height - 1)
                addBlock(0, x, y, 9);
            else {
                addBlock(1, x, y, 0);
            }
}

function example() {
    $.getJSON('example.json', function(data) {
        init_json(data);
    });
}

function init_json(data) {
    width = 200;
    height = 200;
    name = "Untitled World";
    worlddata = [];


    $.each(data, function(key, value) {
        if (key == "width")
            width = parseInt(value);
        if (key == "height")
            height = parseInt(value);

        adjustCanvas();

        if (key == "name")
            name = value;

        if (key == "worlddata") {
            $.each(value, function(index, object) {
                try {
                    var type, layer = 0, x, y, x1, y1, rotation = -1;

                    $.each(object, function(k, v) {
                        switch (k) {
                            case "type":
                                type = v;
                                break;
                            case "layer":
                                layer = v;
                                break;
                            case "x":
                                x = Base64Binary.decode(v);
                                break;
                            case "y":
                                y = Base64Binary.decode(v);
                                break;
                            case "x1":
                                x1 = Base64Binary.decode(v);
                                break;
                            case "y1":
                                y1 = Base64Binary.decode(v);
                                break;
                            case "rotation":
                            case "signtype":
                                rotation = v;
                                break;
                        }
                    });

                    if (x1 != null)
                        for (var j = 0; j < x1.length; j++)
                            worlddata.push({ type: type, layer: layer, x: x1[j], y: y1[j], rotation: rotation });

                    if (x != null)
                        for (var k = 0; k < x.length; k += 2)
                            worlddata.push({ type: type, layer: layer, x: ((x[k] << 8) + x[k + 1]), y: ((y[k] << 8) + y[k + 1]), rotation: rotation });

                } catch (e) {
                    console.log("Exception: " + e);
                }
            });
        }
    });

    if (worlddata.length == 0)
        return;

    clearMap();

    $("#world-name").text('"' + name + '"');

    var bg = [], fg = [];
    worlddata.forEach(function(block) {
        switch (block.layer) {
            case 0:
                fg.push(block);
                break;
            case 1:
                bg.push(block);
                break;
        }
    }, this);

    // draw background blocks before foreground blocks
    bg.forEach(function(block) {
        addBlock(block.layer, block.x, block.y, block.type, block.rotation);
    });

    fg.forEach(function(block) {
        addBlock(block.layer, block.x, block.y, block.type, block.rotation);
    });
}