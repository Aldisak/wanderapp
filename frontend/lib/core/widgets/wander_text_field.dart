import 'package:flutter/material.dart';
import 'package:wandermeet_app/core/tokens/app_colors.dart';
import 'package:wandermeet_app/core/tokens/app_radius.dart';
import 'package:wandermeet_app/core/tokens/app_typography.dart';

/// WanderMeet custom text field.
/// Filled surface, 1 px line border, radius 14, padding 14.
/// Focus: 1.5 px ember border, no glow.
class WanderTextField extends StatefulWidget {
  const WanderTextField({
    super.key,
    this.controller,
    this.hint,
    this.label,
    this.error,
    this.helper,
    this.obscureText = false,
    this.onChanged,
  });

  final TextEditingController? controller;
  final String? hint;
  final String? label;
  final String? error;
  final String? helper;
  final bool obscureText;
  final ValueChanged<String>? onChanged;

  @override
  State<WanderTextField> createState() => _WanderTextFieldState();
}

class _WanderTextFieldState extends State<WanderTextField> {
  final FocusNode _focusNode = FocusNode();
  bool _isFocused = false;

  @override
  void initState() {
    super.initState();
    _focusNode.addListener(_onFocusChange);
  }

  @override
  void dispose() {
    _focusNode.removeListener(_onFocusChange);
    _focusNode.dispose();
    super.dispose();
  }

  void _onFocusChange() {
    setState(() {
      _isFocused = _focusNode.hasFocus;
    });
  }

  @override
  Widget build(BuildContext context) {
    final borderColor = widget.error != null
        ? AppColors.error
        : _isFocused
        ? AppColors.ember
        : AppColors.line;

    final borderWidth = _isFocused ? 1.5 : 1.0;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        if (widget.label != null)
          Padding(
            padding: const EdgeInsets.only(bottom: AppSpace.xxs),
            child: Text(widget.label!, style: AppText.caption),
          ),
        TextField(
          controller: widget.controller,
          focusNode: _focusNode,
          obscureText: widget.obscureText,
          onChanged: widget.onChanged,
          style: AppText.body.copyWith(color: AppColors.ink),
          decoration: InputDecoration(
            hintText: widget.hint,
            hintStyle: AppText.body.copyWith(color: AppColors.ink3),
            contentPadding: const EdgeInsets.all(AppSpace.lg),
            filled: true,
            fillColor: AppColors.surface,
            errorText: widget.error,
            helperText: widget.helper,
            helperStyle: AppText.caption,
            errorStyle: AppText.caption.copyWith(color: AppColors.error),
            border: OutlineInputBorder(
              borderRadius: AppRadius.all14,
              borderSide: BorderSide(color: borderColor, width: borderWidth),
            ),
            enabledBorder: OutlineInputBorder(
              borderRadius: AppRadius.all14,
              borderSide: const BorderSide(color: AppColors.line),
            ),
            focusedBorder: OutlineInputBorder(
              borderRadius: AppRadius.all14,
              borderSide: const BorderSide(color: AppColors.ember, width: 1.5),
            ),
            errorBorder: OutlineInputBorder(
              borderRadius: AppRadius.all14,
              borderSide: const BorderSide(color: AppColors.error),
            ),
            focusedErrorBorder: OutlineInputBorder(
              borderRadius: AppRadius.all14,
              borderSide: const BorderSide(color: AppColors.error, width: 1.5),
            ),
          ),
        ),
      ],
    );
  }
}
