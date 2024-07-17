
def dict_to_css(data, indent=0):
    css = ""

    # Iterate over key-value pairs in the dictionary
    for key, value in data.items():
        if isinstance(value, dict):
            # If the value is a dictionary, recursively process it
            nested_css = dict_to_css(value, indent + 1)
            css += f"{key} {{\n{nested_css}\n{' ' * (indent * 4)}}}\n"
        else:
            # If the value is not a dictionary, assume it's a CSS property and value
            css += f"{' ' * (indent * 4)}{key}: {value};\n"

    return css


def add_target(sheet, target):

    # indent inputted style-sheet
    tabbed_sheet = ["\t" + line for line in sheet.splitlines()]
    tabbed_sheet = "\n".join(tabbed_sheet)

    # assemble it into sheet with a selector
    final = f"{target} {{\n{tabbed_sheet}\n}}"
    return final
