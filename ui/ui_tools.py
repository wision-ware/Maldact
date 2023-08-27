from io import StringIO


def write_css(selector, data, output):
    children = []
    attributes = []

    for key, value in data.items():
        if hasattr(value, 'items'):
            children.append((key, value))
        else:
            attributes.append((key, value))

    if attributes:
        print(' '.join(selector), "{", file=output)
        for key, value in attributes:
            print("\t", key + ":", value, file=output)
        print("}", file=output)

    for key, value in children:
        write_css(selector + (key,), value, output)


def dict_to_css(data):
    output = StringIO()
    write_css((), data, output)
    return output.getvalue()