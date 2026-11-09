package com.adfacten.unity;

/** Implemented in C# (AndroidJavaProxy). Receives every bridge event on the Android UI thread. */
public interface AdfactenUnityListener {
    void onEvent(int handle, String name, String payload);
}
