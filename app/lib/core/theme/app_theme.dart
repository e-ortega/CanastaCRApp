import 'package:flutter/material.dart';

const _teal = Color(0xFF1D9E75);
const _tealLight = Color(0xFFE1F5EE);
const _tealDark = Color(0xFF085041);

class AppTheme {
  static ThemeData get light => ThemeData(
        useMaterial3: true,
        colorScheme: ColorScheme.fromSeed(
          seedColor: _teal,
          primary: _teal,
          onPrimary: Colors.white,
          primaryContainer: _tealLight,
          onPrimaryContainer: _tealDark,
        ),
        appBarTheme: const AppBarTheme(
          backgroundColor: _teal,
          foregroundColor: Colors.white,
          elevation: 0,
        ),
        navigationBarTheme: NavigationBarThemeData(
          indicatorColor: _tealLight,
          iconTheme: WidgetStateProperty.resolveWith((states) {
            if (states.contains(WidgetState.selected)) {
              return const IconThemeData(color: _teal);
            }
            return const IconThemeData(color: Colors.grey);
          }),
          labelTextStyle: WidgetStateProperty.resolveWith((states) {
            final base = const TextStyle(fontSize: 11);
            if (states.contains(WidgetState.selected)) {
              return base.copyWith(color: _teal, fontWeight: FontWeight.w600);
            }
            return base.copyWith(color: Colors.grey);
          }),
        ),
        inputDecorationTheme: InputDecorationTheme(
          filled: true,
          fillColor: Colors.white,
          contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(10),
            borderSide: BorderSide.none,
          ),
          hintStyle: const TextStyle(color: Colors.grey),
        ),
        elevatedButtonTheme: ElevatedButtonThemeData(
          style: ElevatedButton.styleFrom(
            backgroundColor: _teal,
            foregroundColor: Colors.white,
            minimumSize: const Size.fromHeight(48),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
          ),
        ),
      );
}
